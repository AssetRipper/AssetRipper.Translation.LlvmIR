using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AssetRipper.Translation.Cpp;

internal sealed partial class GlobalVariableContext
{
	public GlobalVariableContext(LLVMValueRef globalVariable, ModuleContext module)
	{
		GlobalVariable = globalVariable;
		Module = module;
		DemangledName = LibLLVMSharp.ValueGetDemangledName(globalVariable);
		if (!string.IsNullOrEmpty(DemangledName))
		{
			CleanName = NotAlphanumericRegex.Replace(DemangledName, "_").Trim('_');
		}
		if (string.IsNullOrEmpty(CleanName))
		{
			CleanName = "Variable";
		}
	}

	/// <summary>
	/// The name used from <see cref="GlobalVariable"/>.
	/// </summary>
	public string MangledName => GlobalVariable.Name;
	/// <summary>
	/// The demangled name of the global.
	/// </summary>
	public string? DemangledName { get; }
	/// <summary>
	/// A clean name that might not be unique.
	/// </summary>
	public string CleanName { get; } = "";
	/// <summary>
	/// The unique name used for creating output.
	/// </summary>
	public string Name { get; set; } = "";
	public LLVMValueRef GlobalVariable { get; }
	public ModuleContext Module { get; }
	public bool HasSingleOperand => GlobalVariable.OperandCount == 1;
	public LLVMValueRef Operand => !HasSingleOperand ? default : GlobalVariable.GetOperand(0);
	public unsafe LLVMTypeRef Type => LLVM.GlobalGetValueType(GlobalVariable);
	public MethodDefinition PointerGetMethod { get; set; } = null!;
	public MethodDefinition DataGetMethod { get; set; } = null!;
	public MethodDefinition DataSetMethod { get; set; } = null!;

	public void CreateFields()
	{
		if (!HasSingleOperand)
		{
			return;
		}

		TypeSignature underlyingType = Module.GetTypeSignature(Type);
		TypeSignature pointerType = underlyingType.MakePointerType();

		string name = CleanName;

		FieldDefinition pointerField;
		{
			string pointerName = "variable_" + name;

			pointerField = new FieldDefinition(pointerName, FieldAttributes.Public | FieldAttributes.Static, Module.Definition.CorLibTypeFactory.IntPtr);
			Module.PointerCacheType.Fields.Add(pointerField);
		}

		// Pointer property
		{
			PointerGetMethod = new MethodDefinition("get_" + name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(pointerType));
			Module.GlobalVariablePointersType.Methods.Add(PointerGetMethod);

			PropertyDefinition property = new PropertyDefinition(name, PropertyAttributes.None, PropertySignature.CreateStatic(pointerType));
			Module.GlobalVariablePointersType.Properties.Add(property);
			property.GetMethod = PointerGetMethod;

			PointerGetMethod.CilMethodBody = new(PointerGetMethod);
			CilInstructionCollection instructions = PointerGetMethod.CilMethodBody.Instructions;

			CilInstructionLabel label = new();

			instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Ldsfld, Module.Definition.DefaultImporter.ImportField(typeof(IntPtr).GetField(nameof(IntPtr.Zero))!));
			instructions.Add(CilOpCodes.Bne_Un, label);

			instructions.Add(CilOpCodes.Sizeof, underlyingType.ToTypeDefOrRef());
			instructions.Add(CilOpCodes.Call, Module.Definition.DefaultImporter.ImportMethod(typeof(Marshal).GetMethod(nameof(Marshal.AllocHGlobal), [typeof(int)])!));
			instructions.Add(CilOpCodes.Stsfld, pointerField);

			label.Instruction = instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Ret);

			instructions.OptimizeMacros();
		}

		// Data property
		{
			PropertyDefinition property = new PropertyDefinition(name, PropertyAttributes.None, PropertySignature.CreateStatic(underlyingType));
			Module.GlobalVariablesType.Properties.Add(property);

			DataGetMethod = new MethodDefinition("get_" + name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(underlyingType));
			Module.GlobalVariablesType.Methods.Add(DataGetMethod);

			DataGetMethod.CilMethodBody = new(DataGetMethod);
			{
				CilInstructionCollection instructions = DataGetMethod.CilMethodBody.Instructions;
				instructions.Add(CilOpCodes.Call, PointerGetMethod);
				instructions.AddLoadIndirect(underlyingType);
				instructions.Add(CilOpCodes.Ret);
			}

			DataSetMethod = new MethodDefinition("set_" + name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(Module.Definition.CorLibTypeFactory.Void, underlyingType));
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

			// Field initialization
			{
				MethodDefinition staticConstructor = Module.GlobalVariablesType.GetOrCreateStaticConstructor();

				CilInstructionCollection instructions = staticConstructor.CilMethodBody!.Instructions;
				instructions.Pop();

				switch (Operand.Kind)
				{
					case LLVMValueKind.LLVMConstantDataArrayValueKind:
						{
							ReadOnlySpan<byte> data = LibLLVMSharp.ConstantDataArrayGetData(Operand);

							FieldDefinition field = Module.AddStoredDataField(data.ToArray());

							IMethodDefOrRef createSpan = (IMethodDefOrRef)Module.Definition.DefaultImporter
								.ImportMethod(typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateSpan))!);
							IMethodDescriptor createSpanInstance = createSpan.MakeGenericInstanceMethod(Module.Definition.CorLibTypeFactory.Byte);

							IMethodDefOrRef spanConstructor = (IMethodDefOrRef)Module.Definition.DefaultImporter
								.ImportMethod(typeof(ReadOnlySpan<byte>).GetConstructor([typeof(void*), typeof(int)])!);

							IMethodDescriptor createInlineArray = Module.InlineArrayHelperType.Methods
								.Single(m => m.Name == nameof(InlineArrayHelper.Create))
								.MakeGenericInstanceMethod(underlyingType, Module.Definition.CorLibTypeFactory.Byte);

							ITypeDescriptor spanType = Module.Definition.DefaultImporter
								.ImportType(typeof(ReadOnlySpan<>))
								.MakeGenericInstanceType(Module.Definition.CorLibTypeFactory.Byte);

							CilLocalVariable spanLocal = instructions.AddLocalVariable(spanType.ToTypeSignature());

							//Used when the data is not a byte array
							//instructions.Add(CilOpCodes.Ldtoken, field);
							//instructions.Add(CilOpCodes.Call, createSpanInstance);
							//instructions.Add(CilOpCodes.Stloc, spanLocal);

							instructions.Add(CilOpCodes.Ldloca, spanLocal);
							instructions.Add(CilOpCodes.Ldsflda, field);
							instructions.Add(CilOpCodes.Ldc_I4, data.Length);
							instructions.Add(CilOpCodes.Call, spanConstructor);

							instructions.Add(CilOpCodes.Ldloc, spanLocal);
							instructions.Add(CilOpCodes.Call, createInlineArray);
							instructions.Add(CilOpCodes.Call, DataSetMethod);
						}
						break;
					case LLVMValueKind.LLVMConstantIntValueKind:
						{
							long value = Operand.ConstIntSExt;

							if (underlyingType is CorLibTypeSignature { ElementType: ElementType.I8 })
							{
								instructions.Add(CilOpCodes.Ldc_I8, value);
							}
							else
							{
								instructions.Add(CilOpCodes.Ldc_I4, (int)value);
							}
							instructions.Add(CilOpCodes.Call, DataSetMethod);
						}
						break;
					case LLVMValueKind.LLVMConstantArrayValueKind:
						{
							// Todo: initialization of the field
							LLVMValueRef[] elements = Operand.GetOperands();
						}
						break;
				}

				instructions.Add(CilOpCodes.Ret); // For our earlier Pop

				instructions.OptimizeMacros();
			}
		}
	}

	[GeneratedRegex(@"[^a-zA-Z0-9]")]
	private static partial Regex NotAlphanumericRegex { get; }
}
