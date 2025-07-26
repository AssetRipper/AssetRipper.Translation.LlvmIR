using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR;

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
	public TypeDefinition DeclaringType { get; set; } = null!;
	public MethodDefinition PointerGetMethod { get; set; } = null!;
	public MethodDefinition DataGetMethod { get; set; } = null!;
	public MethodDefinition DataSetMethod { get; set; } = null!;

	public void CreateProperties()
	{
		TypeSignature underlyingType = Module.GetTypeSignature(Type);
		TypeSignature pointerType = underlyingType.MakePointerType();

		DeclaringType = new TypeDefinition(Module.Options.GetNamespace("GlobalVariables"), Name, TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, Module.Definition.CorLibTypeFactory.Object.ToTypeDefOrRef());
		Module.Definition.TopLevelTypes.Add(DeclaringType);
		this.AddNameAttributes(DeclaringType);

		FieldDefinition pointerField;
		{
			pointerField = new FieldDefinition("__pointer", FieldAttributes.Public | FieldAttributes.Static, pointerType);
			DeclaringType.Fields.Add(pointerField);
		}

		// Pointer property
		{
			PointerGetMethod = new MethodDefinition("get_Pointer", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(pointerType));
			DeclaringType.Methods.Add(PointerGetMethod);

			PropertyDefinition property = new("Pointer", PropertyAttributes.None, PropertySignature.CreateStatic(pointerType));
			DeclaringType.Properties.Add(property);
			property.GetMethod = PointerGetMethod;

			PointerGetMethod.CilMethodBody = new(PointerGetMethod);
			CilInstructionCollection instructions = PointerGetMethod.CilMethodBody.Instructions;

			CilInstructionLabel label = new();

			instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Ldc_I4_0);
			instructions.Add(CilOpCodes.Conv_U);
			instructions.Add(CilOpCodes.Bne_Un, label);

			instructions.Add(CilOpCodes.Sizeof, underlyingType.ToTypeDefOrRef());
			instructions.Add(CilOpCodes.Call, Module.Definition.DefaultImporter.ImportMethod(typeof(Marshal).GetMethod(nameof(Marshal.AllocHGlobal), [typeof(int)])!));
			instructions.Add(CilOpCodes.Stsfld, pointerField);

			instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.AddDefaultValue(underlyingType);
			instructions.AddStoreIndirect(underlyingType);

			instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Call, Module.InjectedTypes[typeof(PointerIndices)].GetMethodByName(nameof(PointerIndices.Register)));

			label.Instruction = instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Ret);

			instructions.OptimizeMacros();
		}

		// Data property
		{
			PropertyDefinition property = new("Value", PropertyAttributes.None, PropertySignature.CreateStatic(underlyingType));
			DeclaringType.Properties.Add(property);

			DataGetMethod = new MethodDefinition("get_Value", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(underlyingType));
			DeclaringType.Methods.Add(DataGetMethod);

			DataGetMethod.CilMethodBody = new(DataGetMethod);
			{
				CilInstructionCollection instructions = DataGetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Call, PointerGetMethod);
				instructions.AddLoadIndirect(underlyingType);
				instructions.Add(CilOpCodes.Ret);
			}

			DataSetMethod = new MethodDefinition("set_Value", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(Module.Definition.CorLibTypeFactory.Void, underlyingType));
			DataSetMethod.Parameters[0].GetOrCreateDefinition().Name = "value";
			DeclaringType.Methods.Add(DataSetMethod);

			DataSetMethod.CilMethodBody = new(DataSetMethod);
			{
				CilInstructionCollection instructions = DataSetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Call, PointerGetMethod);
				instructions.Add(CilOpCodes.Ldarg_0);
				instructions.AddStoreIndirect(underlyingType);
				instructions.Add(CilOpCodes.Ret);
			}

			property.SetSemanticMethods(DataGetMethod, DataSetMethod);
		}
	}

	public void InitializeData()
	{
		if (HasSingleOperand)
		{
			MethodDefinition initializerMethod = DeclaringType.AddMethod("Initalize", MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig, Module.Definition.CorLibTypeFactory.Void);
			{
				// https://github.com/icsharpcode/ILSpy/issues/3524

				CilInstructionCollection instructions = initializerMethod.CilMethodBody!.Instructions;

				Module.LoadValue(instructions, Operand);
				instructions.Add(CilOpCodes.Call, DataSetMethod);
				instructions.Add(CilOpCodes.Ret);

				instructions.OptimizeMacros();
			}

			MethodDefinition staticConstructor = DeclaringType.GetOrCreateStaticConstructor();
			{
				CilInstructionCollection instructions = staticConstructor.CilMethodBody!.Instructions;
				instructions.Clear();

				instructions.Add(CilOpCodes.Call, initializerMethod);
				instructions.Add(CilOpCodes.Ret);
			}
		}
	}

	public void AddPublicImplementation()
	{
		TypeSignature underlyingType = Module.GetTypeSignature(Type);

		PropertyDefinition property = new(Name, PropertyAttributes.None, PropertySignature.CreateStatic(underlyingType));
		Module.GlobalMembersType.Properties.Add(property);

		MethodDefinition getMethod = new("get_" + Name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(underlyingType));
		Module.GlobalMembersType.Methods.Add(getMethod);

		getMethod.CilMethodBody = new(getMethod);
		{
			CilInstructionCollection instructions = getMethod.CilMethodBody.Instructions;
			instructions.Add(CilOpCodes.Call, DataGetMethod);
			instructions.Add(CilOpCodes.Ret);
		}

		MethodDefinition setMethod = new("set_" + Name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(Module.Definition.CorLibTypeFactory.Void, underlyingType));
		setMethod.Parameters[0].GetOrCreateDefinition().Name = "value";
		Module.GlobalMembersType.Methods.Add(setMethod);

		setMethod.CilMethodBody = new(setMethod);
		{
			CilInstructionCollection instructions = setMethod.CilMethodBody.Instructions;
			instructions.Add(CilOpCodes.Ldarg_0);
			instructions.Add(CilOpCodes.Call, DataSetMethod);
			instructions.Add(CilOpCodes.Ret);
		}

		property.SetSemanticMethods(getMethod, setMethod);

		this.AddNameAttributes(property);
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

		return NameGenerator.CleanName(result, "Variable").CapitalizeGetOrSet();

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
