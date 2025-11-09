using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR;

internal sealed class EnumContext : IHasName
{
	public ModuleContext Module { get; }
	public TypeDefinition Definition { get; }
	public LLVMMetadataRef Metadata { get; }
	public LLVMMetadataRef[] Elements { get; }

	public string MangledName => Metadata.Identifier;

	public string? DemangledName => Metadata.IdentifierDemangled;

	public string CleanName { get; }

	public string Name { get => Definition.Name ?? ""; set => Definition.Name = value; }

	string? IHasName.NativeType => null;

	private EnumContext(ModuleContext module, TypeDefinition definition, LLVMMetadataRef metadata)
	{
		Module = module;
		Definition = definition;
		Metadata = metadata;
		Elements = metadata.Elements;
		string name = metadata.Name;
		if (string.IsNullOrEmpty(name) && !metadata.IdentifierDemangled.Contains(">:"))
		{
			// Handle unnamed enums.
			// The >: check avoids names from templates, which tend to be long and worse than just using Enum_hash.
			string cleanIdentifier = metadata.IdentifierClean;
			int index = cleanIdentifier.LastIndexOf("<unnamed-enum-");
			name = index >= 0 ? $"{cleanIdentifier.AsSpan(0, index)}_unnamed_enum" : cleanIdentifier;
		}
		CleanName = NameGenerator.CleanName(name, "Enum");
	}

	public static EnumContext Create(ModuleContext module, LLVMMetadataRef metadata)
	{
		if (metadata.Kind != LLVMMetadataKind.LLVMDICompositeTypeMetadataKind || metadata.TagString is not "DW_TAG_enumeration_type")
		{
			throw new ArgumentException("Metadata is not an enumeration type.", nameof(metadata));
		}

		TypeDefinition typeDefinition = new(
			module.Options.GetNamespace("Enumerations"),
			$"{metadata.Name}_{Guid.NewGuid()}",
			TypeAttributes.Public | TypeAttributes.Sealed,
			module.Definition.DefaultImporter.ImportType(typeof(Enum)));
		module.Definition.TopLevelTypes.Add(typeDefinition);
		EnumContext enumContext = new(module, typeDefinition, metadata);

		ElementType elementType = metadata.SizeInBits switch
		{
			8 => ElementType.I1,
			16 => ElementType.I2,
			32 => ElementType.I4,
			64 => ElementType.I8,
			_ => throw new NotSupportedException($"Enum with size {metadata.SizeInBits} bits is not supported."),
		};

		{
			FieldDefinition field = new("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RuntimeSpecialName, module.Definition.CorLibTypeFactory.FromElementType(elementType));
			typeDefinition.Fields.Add(field);
		}

		TypeSignature fieldType = typeDefinition.ToTypeSignature();
		foreach (LLVMMetadataRef element in metadata.Elements)
		{
			if (element.Kind != LLVMMetadataKind.LLVMDIEnumeratorMetadataKind)
			{
				continue;
			}
			string name = element.Name;
			if (string.IsNullOrEmpty(name))
			{
				continue;
			}
			long value = LibLLVMSharp.DIEnumeratorGetValueSExt(element);
			FieldDefinition field = new(name, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, fieldType);
			field.Constant = elementType switch
			{
				ElementType.I1 => Constant.FromValue((sbyte)value),
				ElementType.I2 => Constant.FromValue((short)value),
				ElementType.I4 => Constant.FromValue((int)value),
				ElementType.I8 => Constant.FromValue(value),
				_ => throw new NotSupportedException($"Enum with size {metadata.SizeInBits} bits is not supported."),
			};
			typeDefinition.Fields.Add(field);
		}
		return enumContext;
	}
}
