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

internal sealed partial class GlobalVariableContext : IHasName
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

	public void CreateFields()
	{
		if (!HasSingleOperand)
		{
			return;
		}

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
							LLVMValueRef[] elements = Operand.GetOperands();

							(TypeSignature elementType, int elementCount) = Module.InlineArrayTypes[(TypeDefinition)underlyingType.ToTypeDefOrRef()];

							if (elementCount != elements.Length)
							{
								throw new Exception("Array element count mismatch");
							}

							if (elementType is PointerTypeSignature)
							{
								elementType = Module.Definition.CorLibTypeFactory.IntPtr;
							}

							TypeSignature spanType = Module.Definition.DefaultImporter
								.ImportType(typeof(Span<>))
								.MakeGenericInstanceType(elementType);

							IMethodDescriptor inlineArrayAsSpan = Module.InlineArrayHelperType.Methods
								.Single(m => m.Name == nameof(InlineArrayHelper.InlineArrayAsSpan))
								.MakeGenericInstanceMethod(underlyingType, elementType);

							MethodSignature getItemSignature = MethodSignature.CreateInstance(new GenericParameterSignature(GenericParameterType.Type, 0).MakeByReferenceType(), Module.Definition.CorLibTypeFactory.Int32);
							IMethodDescriptor getItem = new MemberReference(spanType.ToTypeDefOrRef(), "get_Item", getItemSignature);

							CilLocalVariable bufferLocal = instructions.AddLocalVariable(underlyingType);
							CilLocalVariable spanLocal = instructions.AddLocalVariable(spanType);

							instructions.AddDefaultValue(underlyingType);
							instructions.Add(CilOpCodes.Stloc, bufferLocal);

							instructions.Add(CilOpCodes.Ldloca, bufferLocal);
							instructions.Add(CilOpCodes.Ldc_I4, elementCount);
							instructions.Add(CilOpCodes.Call, inlineArrayAsSpan);
							instructions.Add(CilOpCodes.Stloc, spanLocal);

							for (int i = 0; i < elements.Length; i++)
							{
								LLVMValueRef element = elements[i];
								instructions.Add(CilOpCodes.Ldloca, spanLocal);
								instructions.Add(CilOpCodes.Ldc_I4, i);
								instructions.Add(CilOpCodes.Call, getItem);
								Module.LoadValue(instructions, element);
								instructions.AddStoreIndirect(elementType);
							}

							instructions.Add(CilOpCodes.Ldloc, bufferLocal);
							instructions.Add(CilOpCodes.Call, DataSetMethod);
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
