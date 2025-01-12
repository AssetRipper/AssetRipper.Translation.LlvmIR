using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal sealed class StructContext
{
	public LLVMTypeRef Type { get; }
	public TypeDefinition Definition { get; }
	public FieldDefinition[] Fields { get; }
	public LLVMTypeRef[] FieldTypes { get; }
	public ModuleContext Module { get; }
	public ulong Size => Type.GetABISize(Module.Module);

	public StructContext(LLVMTypeRef type, TypeDefinition definition, FieldDefinition[] fields, LLVMTypeRef[] fieldTypes, ModuleContext module)
	{
		Debug.Assert(fields.Length == fieldTypes.Length);
		Type = type;
		Definition = definition;
		Fields = fields;
		FieldTypes = fieldTypes;
		Module = module;
	}

	public static StructContext Create(string name, LLVMTypeRef type, ModuleContext module)
	{
		Debug.Assert(type.Kind == LLVMTypeKind.LLVMStructTypeKind);

		TypeDefinition typeDefinition = new(
			null,
			name,
			TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit,
			module.Definition.DefaultImporter.ImportType(typeof(ValueType)));
		module.Definition.TopLevelTypes.Add(typeDefinition);

		LLVMTypeRef[] fieldTypes = type.GetSubtypes();
		FieldDefinition[] fields = new FieldDefinition[fieldTypes.Length];
		for (int i = 0; i < fieldTypes.Length; i++)
		{
			string fieldName = $"field_{i}";
			FieldDefinition field = new(fieldName, FieldAttributes.Public, (FieldSignature?)null);
			typeDefinition.Fields.Add(field);
			fields[i] = field;
		}

		return new StructContext(type, typeDefinition, fields, fieldTypes, module);
	}

	public TypeSignature? GetFieldTypeSignature(int index)
	{
		return Fields[index].Signature?.FieldType;
	}

	public void SetFieldTypeSignature(int index, TypeSignature type)
	{
		Fields[index].Signature = new FieldSignature(type);
	}

	public void SetKnownFieldTypeSignatures()
	{
		for (int i = 0; i < Fields.Length; i++)
		{
			TypeSignature? fieldType = Module.GetExactTypeSignature(FieldTypes[i]);
			if (fieldType is not null)
			{
				SetFieldTypeSignature(i, fieldType);
			}
		}
	}
}
