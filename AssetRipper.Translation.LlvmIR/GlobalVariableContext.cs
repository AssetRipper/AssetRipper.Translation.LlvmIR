﻿using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Instructions;
using AssetRipper.Translation.LlvmIR.Variables;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.LlvmIR;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class GlobalVariableContext : IHasName, IVariable
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
	public TypeSignature PointerType => PointerField?.Signature?.FieldType ?? DataGetMethod.Signature!.ReturnType.MakePointerType();
	TypeSignature IVariable.VariableType => DataGetMethod.Signature!.ReturnType;
	bool IVariable.SupportsLoadAddress => true;
	public TypeDefinition DeclaringType { get; set; } = null!;
	private FieldDefinition PointerField { get; set; } = null!;
	public MethodDefinition DataGetMethod { get; set; } = null!;
	public MethodDefinition DataSetMethod { get; set; } = null!;
	private bool PointerIsUsed { get; set; } = false;
	/// <summary>
	/// The number of instructions in the static constructor for initializing the pointer field.
	/// </summary>
	private int PointerInstructionsCount { get; set; } = 0;

	public void CreateProperties()
	{
		TypeSignature underlyingType = Module.GetTypeSignature(Type);
		TypeSignature pointerType = underlyingType.MakePointerType();

		DeclaringType = new TypeDefinition(Module.Options.GetNamespace("GlobalVariables"), Name, TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed, Module.Definition.CorLibTypeFactory.Object.ToTypeDefOrRef());
		Module.Definition.TopLevelTypes.Add(DeclaringType);
		this.AddNameAttributes(DeclaringType);

		// Pointer field
		{
			PointerField = new FieldDefinition("__pointer", FieldAttributes.Public | FieldAttributes.Static, pointerType);
			DeclaringType.Fields.Add(PointerField);
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
				instructions.Add(CilOpCodes.Ldsfld, PointerField);
				instructions.AddLoadIndirect(underlyingType);
				instructions.Add(CilOpCodes.Ret);
			}

			DataSetMethod = new MethodDefinition("set_Value", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(Module.Definition.CorLibTypeFactory.Void, underlyingType));
			DataSetMethod.Parameters[0].GetOrCreateDefinition().Name = "value";
			DeclaringType.Methods.Add(DataSetMethod);

			DataSetMethod.CilMethodBody = new(DataSetMethod);
			{
				CilInstructionCollection instructions = DataSetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Ldsfld, PointerField);
				instructions.Add(CilOpCodes.Ldarg_0);
				instructions.AddStoreIndirect(underlyingType);
				instructions.Add(CilOpCodes.Ret);
			}

			property.SetSemanticMethods(DataGetMethod, DataSetMethod);
		}
	}

	public void InitializeData()
	{
		TypeSignature underlyingType = Module.GetTypeSignature(Type);
		MethodDefinition staticConstructor = DeclaringType.GetOrCreateStaticConstructor();

		CilInstructionCollection instructions = staticConstructor.CilMethodBody!.Instructions;
		instructions.Clear();

		// Initialize pointer field
		{
			instructions.Add(CilOpCodes.Sizeof, underlyingType.ToTypeDefOrRef());
			instructions.Add(CilOpCodes.Call, Module.InjectedTypes[typeof(NativeMemoryHelper)].Methods.First(m =>
			{
				return m.Name == nameof(NativeMemoryHelper.Allocate) && m.Parameters[0].ParameterType.ElementType is ElementType.I4;
			}));
			instructions.Add(CilOpCodes.Call, Module.InjectedTypes[typeof(PointerIndices)].GetMethodByName(nameof(PointerIndices.Register)));
			instructions.Add(CilOpCodes.Stsfld, PointerField);
		}

		// Initialize data
		if (HasSingleOperand)
		{
			PointerInstructionsCount = instructions.Count;

			BasicBlock basicBlock = InstructionLifter.Initialize(this);
			InstructionOptimizer.Optimize([basicBlock]);
			basicBlock.AddInstructions(instructions);
		}
		else
		{
			instructions.AddDefaultValue(underlyingType);
			instructions.Add(CilOpCodes.Call, DataSetMethod);

			PointerInstructionsCount = instructions.Count;
		}

		instructions.Add(CilOpCodes.Ret);

		instructions.OptimizeMacros();
	}

	public void AddPublicImplementation()
	{
		TypeSignature underlyingType = DataGetMethod.Signature!.ReturnType;

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

	void IVariable.AddLoad(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Call, DataGetMethod);
	}

	void IVariable.AddStore(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Call, DataSetMethod);
	}

	public void AddLoadAddress(CilInstructionCollection instructions)
	{
		PointerIsUsed = true;
		instructions.Add(CilOpCodes.Ldsfld, PointerField);
	}

	public void RemovePointerFieldIfNotUsed()
	{
		if (!PointerIsUsed)
		{
			DeclaringType.Fields.Remove(PointerField);
			PointerField = null!;
			DeclaringType.GetStaticConstructor()!.CilMethodBody!.Instructions.RemoveRange(0, PointerInstructionsCount);
			PointerInstructionsCount = 0;

			FieldDefinition backingField = new("__value", FieldAttributes.Private | FieldAttributes.Static, DataGetMethod.Signature!.ReturnType);
			DeclaringType.Fields.Add(backingField);

			// Update the get method
			{
				CilInstructionCollection instructions = DataGetMethod.CilMethodBody!.Instructions;
				instructions.Clear();
				instructions.Add(CilOpCodes.Ldsfld, backingField);
				instructions.Add(CilOpCodes.Ret);
			}

			// Update the set method
			{
				CilInstructionCollection instructions = DataSetMethod.CilMethodBody!.Instructions;
				instructions.Clear();
				instructions.Add(CilOpCodes.Ldarg_0);
				instructions.Add(CilOpCodes.Stsfld, backingField);
				instructions.Add(CilOpCodes.Ret);
			}
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
