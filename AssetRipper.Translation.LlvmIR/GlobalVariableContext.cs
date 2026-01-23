using AsmResolver.DotNet;
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
using System.Runtime.CompilerServices;

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
	string? IHasName.NativeType => null;
	public LLVMValueRef GlobalVariable { get; }
	public ModuleContext Module { get; }
	public bool HasSingleOperand => GlobalVariable.OperandCount == 1;
	public LLVMValueRef Operand => !HasSingleOperand ? default : GlobalVariable.GetOperand(0);
	public unsafe LLVMTypeRef Type => LLVM.GlobalGetValueType(GlobalVariable);
	public TypeSignature DataType => DataGetMethod.Signature!.ReturnType;
	public TypeSignature PointerType => PointerMethod?.Signature?.ReturnType ?? DataType.MakePointerType();
	TypeSignature IVariable.VariableType => DataType;
	bool IVariable.SupportsLoadAddress => true;
	public TypeDefinition DeclaringType { get; set; } = null!;
	private FieldDefinition DataField { get; set; } = null!;
	private MethodDefinition PointerMethod { get; set; } = null!;
	private MethodDefinition DataGetMethod { get; set; } = null!;
	private MethodDefinition DataSetMethod { get; set; } = null!;

	public void CreateProperties()
	{
		TypeSignature underlyingType = Module.GetTypeSignature(Type);

		DeclaringType = new TypeDefinition(Module.Options.GetNamespace("GlobalVariables"), Name, TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed, Module.Definition.CorLibTypeFactory.Object.ToTypeDefOrRef());
		Module.Definition.TopLevelTypes.Add(DeclaringType);
		this.AddNameAttributes(DeclaringType);

		// Data field
		{
			// Note: the field type might be changed later if it needs a fixed address.
			DataField = new("__value", FieldAttributes.Private | FieldAttributes.Static, underlyingType);
			DeclaringType.Fields.Add(DataField);
		}

		// Data property
		{
			PropertyDefinition property = new("Value", PropertyAttributes.None, PropertySignature.CreateStatic(underlyingType));
			DeclaringType.Properties.Add(property);

			DataGetMethod = new MethodDefinition("get_Value", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(underlyingType));
			DeclaringType.Methods.Add(DataGetMethod);

			DataSetMethod = new MethodDefinition("set_Value", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(Module.Definition.CorLibTypeFactory.Void, underlyingType));
			DataSetMethod.Parameters[0].GetOrCreateDefinition().Name = "value";
			DeclaringType.Methods.Add(DataSetMethod);

			property.SetSemanticMethods(DataGetMethod, DataSetMethod);
		}
	}

	public void InitializeData()
	{
		MethodDefinition staticConstructor = DeclaringType.GetOrCreateStaticConstructor();

		CilInstructionCollection instructions = staticConstructor.CilMethodBody!.Instructions;
		instructions.Clear();

		// Initialize data
		if (HasSingleOperand)
		{
			BasicBlock basicBlock = InstructionLifter.Initialize(this);
			InstructionOptimizer.Optimize([basicBlock]);
			basicBlock.AddInstructions(instructions);
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

		getMethod.CilMethodBody = new();
		{
			CilInstructionCollection instructions = getMethod.CilMethodBody.Instructions;
			instructions.Add(CilOpCodes.Call, DataGetMethod);
			instructions.Add(CilOpCodes.Ret);
		}

		MethodDefinition setMethod = new("set_" + Name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(Module.Definition.CorLibTypeFactory.Void, underlyingType));
		setMethod.Parameters[0].GetOrCreateDefinition().Name = "value";
		Module.GlobalMembersType.Methods.Add(setMethod);

		setMethod.CilMethodBody = new();
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
		if (PointerMethod is null)
		{
			TypeSignature returnType = PointerType;
			PropertyDefinition property = new("Pointer", PropertyAttributes.None, PropertySignature.CreateStatic(returnType));
			DeclaringType.Properties.Insert(0, property); // Prefer to have Pointer property first
			PointerMethod = new MethodDefinition("get_Pointer", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(returnType));
			DeclaringType.Methods.Add(PointerMethod);
			PointerMethod.CilMethodBody = new();
			{
				CilInstructionCollection instructions2 = PointerMethod.CilMethodBody.Instructions;
				instructions2.Add(CilOpCodes.Ldsflda, DataField);
				instructions2.Add(CilOpCodes.Ret);
			}
			property.SetSemanticMethods(PointerMethod, null);
		}
		instructions.Add(CilOpCodes.Call, PointerMethod);
	}

	public void RemovePointerFieldIfNotUsed()
	{
		if (PointerMethod is not null)
		{
			// Add FixedAddressValueType attribute
			{
				// https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.fixedaddressvaluetypeattribute

				// Despite the name, this attribute is applicable to fields of any type, not just value types.
				// The name seems to originate from .NET Framework. When I tested this attribute with the documented example code on .NET Framework 4.7.2,
				// unattributed static fields with struct types were not given fixed addresses. However, primitive types, reference types, and pointer types
				// were given fixed addresses even without the attribute.

				// When I tested the same example code on .NET 10, all static fields were given fixed addresses regardless of the attribute.
				// This behavior is supposedly an implementation detail and should not be relied upon, so we add the attribute to enforce a fixed address,
				// in case a future version of .NET changes the default behavior for fields without the attribute.

				System.Reflection.ConstructorInfo constructorInfo = typeof(FixedAddressValueTypeAttribute).GetConstructors().Single();
				IMethodDescriptor inlineArrayAttributeConstructor = Module.Definition.DefaultImporter.ImportMethod(constructorInfo);
				DataField.CustomAttributes.Add(new CustomAttribute((ICustomAttributeType)inlineArrayAttributeConstructor, new CustomAttributeSignature()));
			}

			// Register pointer in static constructor
			{
				CilInstructionCollection instructions = DeclaringType.GetStaticConstructor()!.CilMethodBody!.Instructions;

				// Pop return instruction
				instructions.Pop();

				instructions.Add(CilOpCodes.Call, PointerMethod);
				instructions.Add(CilOpCodes.Call, Module.InjectedTypes[typeof(PointerIndices)].GetMethodByName(nameof(PointerIndices.Register)));
				instructions.Add(CilOpCodes.Pop);

				instructions.Add(CilOpCodes.Ret);
			}
		}

		if (PointerMethod is not null && DataType is PointerTypeSignature or CorLibTypeSignature)
		{
			// Static fields cannot be pinned if they are not a struct type, so we need to change the field type to a struct type.

			TypeDefinition wrapperType = new(
				null,
				"__WrapperType",
				TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass,
				Module.Definition.DefaultImporter.ImportType(typeof(ValueType)));
			DeclaringType.NestedTypes.Add(wrapperType);

			FieldDefinition pointerField = new("__value", FieldAttributes.Public, DataType);
			wrapperType.Fields.Add(pointerField);

			DataField.Signature!.FieldType = wrapperType.ToTypeSignature();

			DataGetMethod.CilMethodBody = new();
			{
				CilInstructionCollection instructions = DataGetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Ldsflda, DataField);
				instructions.Add(CilOpCodes.Ldfld, pointerField);
				instructions.Add(CilOpCodes.Ret);
			}
			DataSetMethod.CilMethodBody = new();
			{
				CilInstructionCollection instructions = DataSetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Ldsflda, DataField);
				instructions.Add(CilOpCodes.Ldarg_0);
				instructions.Add(CilOpCodes.Stfld, pointerField);
				instructions.Add(CilOpCodes.Ret);
			}
		}
		else
		{
			DataGetMethod.CilMethodBody = new();
			{
				CilInstructionCollection instructions = DataGetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Ldsfld, DataField);
				instructions.Add(CilOpCodes.Ret);
			}
			DataSetMethod.CilMethodBody = new();
			{
				CilInstructionCollection instructions = DataSetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Ldarg_0);
				instructions.Add(CilOpCodes.Stsfld, DataField);
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
