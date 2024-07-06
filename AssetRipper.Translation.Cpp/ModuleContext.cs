using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp;

internal sealed class ModuleContext
{
	public ModuleContext(LLVMModuleRef module, ModuleDefinition definition)
	{
		Module = module;
		Definition = definition;
	}

	public LLVMModuleRef Module { get; }
	public ModuleDefinition Definition { get; }
	public Dictionary<LLVMValueRef, FunctionContext> Methods { get; } = new();
	public Dictionary<string, TypeDefinition> Structs { get; } = new();

	public TypeSignature GetTypeSignature(LLVMTypeRef type)
	{
		switch (type.Kind)
		{
			case LLVMTypeKind.LLVMVoidTypeKind:
				return Definition.CorLibTypeFactory.Void;
			case LLVMTypeKind.LLVMHalfTypeKind:
				return Definition.DefaultImporter.ImportTypeSignature(typeof(Half));
			case LLVMTypeKind.LLVMFloatTypeKind:
				return Definition.CorLibTypeFactory.Single;
			case LLVMTypeKind.LLVMDoubleTypeKind:
				return Definition.CorLibTypeFactory.Double;
			case LLVMTypeKind.LLVMX86_FP80TypeKind:
				//x86_fp80 has a very unique structure and uses 10 bytes
				goto default;
			case LLVMTypeKind.LLVMFP128TypeKind:
				//IEEE 754 floating point number with 128 bits.
				goto default;
			case LLVMTypeKind.LLVMPPC_FP128TypeKind:
				//ppc_fp128 can be approximated by fp128, which conforms to IEEE 754 standards.
				goto case LLVMTypeKind.LLVMFP128TypeKind;
			case LLVMTypeKind.LLVMLabelTypeKind:
				goto default;
			case LLVMTypeKind.LLVMIntegerTypeKind:
				return type.IntWidth switch
				{
					//Note: non-powers of 2 are valid and might be used for bitfields.
					1 => Definition.CorLibTypeFactory.Boolean,
					8 => Definition.CorLibTypeFactory.SByte,
					16 => Definition.CorLibTypeFactory.Int16,
					32 => Definition.CorLibTypeFactory.Int32,
					64 => Definition.CorLibTypeFactory.Int64,
					//128
					_ => throw new NotSupportedException(),
				};
			case LLVMTypeKind.LLVMFunctionTypeKind:
				//I don't think this can happen, but void* is fine.
				return Definition.CorLibTypeFactory.Void.MakePointerType();
			case LLVMTypeKind.LLVMStructTypeKind:
				{
					string name = type.StructName;
					if (!Structs.TryGetValue(name, out TypeDefinition? typeDefinition))
					{
						typeDefinition = new(
							null,
							name,
							TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit,
							Definition.DefaultImporter.ImportType(typeof(ValueType)));
						Definition.TopLevelTypes.Add(typeDefinition);
						Structs.Add(name, typeDefinition);

						LLVMTypeRef[] array = type.GetSubtypes();
						for (int i = 0; i < array.Length; i++)
						{
							LLVMTypeRef subType = array[i];
							TypeSignature fieldType = GetTypeSignature(subType);
							string fieldName = $"field_{i}";
							FieldDefinition field = new(fieldName, FieldAttributes.Public, fieldType);
							typeDefinition.Fields.Add(field);
						}
					}
					return typeDefinition.ToTypeSignature();
				}
			case LLVMTypeKind.LLVMArrayTypeKind:
				goto default;
			case LLVMTypeKind.LLVMPointerTypeKind:
				//All pointers are void* in IR
				return Definition.CorLibTypeFactory.Void.MakePointerType();
			case LLVMTypeKind.LLVMVectorTypeKind:
				goto default;
			case LLVMTypeKind.LLVMMetadataTypeKind:
				goto default;
			case LLVMTypeKind.LLVMX86_MMXTypeKind:
				goto default;
			case LLVMTypeKind.LLVMTokenTypeKind:
				goto default;
			case LLVMTypeKind.LLVMScalableVectorTypeKind:
				goto default;
			case LLVMTypeKind.LLVMBFloatTypeKind:
				//Half is just an approximation of BFloat16, which is not yet supported in .NET
				//Maybe we can use this instead: https://www.nuget.org/packages/UltimateOrb.TruncatedFloatingPoints
				return Definition.DefaultImporter.ImportTypeSignature(typeof(Half));
			case LLVMTypeKind.LLVMX86_AMXTypeKind:
				goto default;
			case LLVMTypeKind.LLVMTargetExtTypeKind:
				goto default;
			default:
				throw new NotSupportedException();
		}
	}
}
