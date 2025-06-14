using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.Cpp;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class GlobalVariableContext : IHasName
{
	public GlobalVariableContext(LLVMValueRef globalVariable, ModuleContext module)
	{
		GlobalVariable = globalVariable;
		Module = module;
		DemangledName = LibLLVMSharp.ValueGetDemangledName(globalVariable);
		CleanName = ExtractCleanName(MangledName, DemangledName, module.Options.RenamedSymbols);
	}

	/// <inheritdoc/>
	public string MangledName => GlobalVariable.Name;
	/// <inheritdoc/>
	public string? DemangledName { get; }
	/// <inheritdoc/>
	public string CleanName { get; }
	/// <inheritdoc/>
	public string Name { get; set; } = "";
	public LLVMValueRef GlobalVariable { get; }
	public ModuleContext Module { get; }
	public bool HasSingleOperand => GlobalVariable.OperandCount == 1;
	public LLVMValueRef Operand => !HasSingleOperand ? default : GlobalVariable.GetOperand(0);
	public unsafe LLVMTypeRef Type => LLVM.GlobalGetValueType(GlobalVariable);
	public MethodDefinition PointerGetMethod { get; set; } = null!;
	public MethodDefinition DataGetMethod { get; set; } = null!;
	public MethodDefinition DataSetMethod { get; set; } = null!;

	public void CreateProperties()
	{
		TypeSignature underlyingType = Module.GetTypeSignature(Type);
		TypeSignature pointerType = underlyingType.MakePointerType();


		FieldDefinition pointerField;
		{
			string pointerName = "variable_" + Name;

			pointerField = new FieldDefinition(pointerName, FieldAttributes.Public | FieldAttributes.Static, Module.Definition.CorLibTypeFactory.IntPtr);
			Module.PointerCacheType.Fields.Add(pointerField);
		}

		// Pointer property
		{
			PointerGetMethod = new MethodDefinition("get_" + Name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(pointerType));
			Module.GlobalVariablePointersType.Methods.Add(PointerGetMethod);

			PropertyDefinition property = new PropertyDefinition(Name, PropertyAttributes.None, PropertySignature.CreateStatic(pointerType));
			Module.GlobalVariablePointersType.Properties.Add(property);
			property.GetMethod = PointerGetMethod;

			PointerGetMethod.CilMethodBody = new(PointerGetMethod);
			CilInstructionCollection instructions = PointerGetMethod.CilMethodBody.Instructions;

			CilLocalVariable variable = instructions.AddLocalVariable(pointerType);

			CilInstructionLabel label = new();

			instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Ldsfld, Module.Definition.DefaultImporter.ImportField(typeof(IntPtr).GetField(nameof(IntPtr.Zero))!));
			instructions.Add(CilOpCodes.Bne_Un, label);

			instructions.Add(CilOpCodes.Sizeof, underlyingType.ToTypeDefOrRef());
			instructions.Add(CilOpCodes.Call, Module.Definition.DefaultImporter.ImportMethod(typeof(Marshal).GetMethod(nameof(Marshal.AllocHGlobal), [typeof(int)])!));
			instructions.Add(CilOpCodes.Stloc, variable);

			instructions.Add(CilOpCodes.Ldloc, variable);
			instructions.AddDefaultValue(underlyingType);
			instructions.AddStoreIndirect(underlyingType);

			instructions.Add(CilOpCodes.Ldloc, variable);
			instructions.Add(CilOpCodes.Stsfld, pointerField);

			label.Instruction = instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Ret);

			instructions.OptimizeMacros();

			this.AddNameAttributes(property);
		}

		// Data property
		{
			PropertyDefinition property = new PropertyDefinition(Name, PropertyAttributes.None, PropertySignature.CreateStatic(underlyingType));
			Module.GlobalVariablesType.Properties.Add(property);

			DataGetMethod = new MethodDefinition("get_" + Name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(underlyingType));
			Module.GlobalVariablesType.Methods.Add(DataGetMethod);

			DataGetMethod.CilMethodBody = new(DataGetMethod);
			{
				CilInstructionCollection instructions = DataGetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Call, PointerGetMethod);
				instructions.AddLoadIndirect(underlyingType);
				instructions.Add(CilOpCodes.Ret);
			}

			DataSetMethod = new MethodDefinition("set_" + Name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(Module.Definition.CorLibTypeFactory.Void, underlyingType));
			Module.GlobalVariablesType.Methods.Add(DataSetMethod);

			DataSetMethod.CilMethodBody = new(DataSetMethod);
			{
				CilInstructionCollection instructions = DataSetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Call, PointerGetMethod);
				instructions.Add(CilOpCodes.Ldarg_0);
				instructions.AddStoreIndirect(underlyingType);
				instructions.Add(CilOpCodes.Ret);
			}

			property.SetSemanticMethods(DataGetMethod, DataSetMethod);

			this.AddNameAttributes(property);
		}
	}

	public void InitializeData()
	{
		if (HasSingleOperand)
		{
			MethodDefinition staticConstructor = Module.GlobalVariablesType.GetOrCreateStaticConstructor();

			CilInstructionCollection instructions = staticConstructor.CilMethodBody!.Instructions;
			instructions.Pop();

			Module.LoadValue(instructions, Operand);
			instructions.Add(CilOpCodes.Call, DataSetMethod);

			instructions.Add(CilOpCodes.Ret); // Undo the pop
		}
	}

	private static string ExtractCleanName(string mangledName, string? demangledName, Dictionary<string, string> renamedSymbols)
	{
		if (renamedSymbols.TryGetValue(mangledName, out string? result))
		{
			if (!NameGenerator.IsValidCSharpName(result))
			{
				throw new ArgumentException($"Renamed symbol '{mangledName}' has an invalid name '{result}'.", nameof(renamedSymbols));
			}
			return result;
		}

		if (mangledName.StartsWith("??_C@", StringComparison.Ordinal))
		{
			return "String"; // Not certain this is just strings
		}

		if (TryGetNameFromBeginning(mangledName, out result))
		{
		}
		else if (TryGetNameFromEnd(mangledName, out result))
		{
		}
		else
		{
			result = demangledName ?? "";
		}

		return NameGenerator.CleanName(result, "Variable");

		static bool TryGetNameFromBeginning(string mangledName, [NotNullWhen(true)] out string? result)
		{
			int questionMarkIndex = mangledName.IndexOf('?');
			int atIndex = mangledName.IndexOf('@');
			if (questionMarkIndex == 0 && atIndex > 0)
			{
				result = mangledName[(questionMarkIndex + 1)..atIndex];
				return result.Length > 0;
			}
			else
			{
				result = null;
				return false;
			}
		}

		static bool TryGetNameFromEnd(string mangledName, [NotNullWhen(true)] out string? result)
		{
			int periodIndex = mangledName.LastIndexOf('.');
			if (mangledName.StartsWith("__const.", StringComparison.Ordinal))
			{
				result = mangledName[(periodIndex + 1)..];
				return result.Length > 0;
			}
			else
			{
				result = null;
				return false;
			}
		}
	}

	private string GetDebuggerDisplay()
	{
		return Name;
	}
}
