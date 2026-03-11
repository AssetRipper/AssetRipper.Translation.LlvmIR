using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class LLVMMetadataRefExtensions
{
	extension(LLVMMetadataRef metadata)
	{
		public bool IsEnum => metadata.Kind is LLVMMetadataKind.LLVMDICompositeTypeMetadataKind && metadata.TagString is "DW_TAG_enumeration_type";

		public bool IsStruct => metadata.Kind is LLVMMetadataKind.LLVMDICompositeTypeMetadataKind && metadata.TagString is "DW_TAG_structure_type";

		public bool IsClass => metadata.Kind is LLVMMetadataKind.LLVMDICompositeTypeMetadataKind && metadata.TagString is "DW_TAG_class_type";

		public bool IsUnion => metadata.Kind is LLVMMetadataKind.LLVMDICompositeTypeMetadataKind && metadata.TagString is "DW_TAG_union_type";

		public bool IsArray => metadata.Kind is LLVMMetadataKind.LLVMDICompositeTypeMetadataKind && metadata.TagString is "DW_TAG_array_type";

		public bool IsPointer => metadata.Kind is LLVMMetadataKind.LLVMDIDerivedTypeMetadataKind && metadata.TagString is "DW_TAG_pointer_type";

		public long ArrayLength
		{
			get
			{
				if (!metadata.IsArray)
				{
					return default;
				}

				LLVMMetadataRef[] elements = metadata.Elements;
				if (elements.Length != 1)
				{
					return default;
				}

				return elements[0].Count.ConstIntSExt;
			}
		}

		public LLVMMetadataRef[] Elements => metadata.IsADICompositeType != default ? llvmsharp.DICompositeType_getElements(metadata) : [];

		public string IdentifierDemangled
		{
			get
			{
				string identifier = metadata.Identifier;
				if (string.IsNullOrEmpty(identifier))
				{
					return "";
				}
				return llvmsharp.Demangle(identifier).RemoveSuffix(" `RTTI Type Descriptor Name'");
			}
		}

		public string IdentifierClean
		{
			get
			{
				string demangled = metadata.IdentifierDemangled;
				if (string.IsNullOrEmpty(demangled))
				{
					return "";
				}

				if (DemangledNamesParser.ParseType(demangled, out string? cleanType))
				{
					return cleanType;
				}
				else
				{
					return "";
				}
			}
		}

		public IEnumerable<LLVMMetadataRef> Members => metadata.Elements.Where(e => e.TagString is "DW_TAG_member");

		public LLVMMetadataRef PassThroughToBaseTypeIfNecessary()
		{
			if (metadata.IsADIDerivedType == default)
			{
				return metadata;
			}

			if (metadata.TagString is "DW_TAG_typedef" or "DW_TAG_const_type" or "DW_TAG_volatile_type" or "DW_TAG_restrict_type")
			{
				return metadata.BaseType.PassThroughToBaseTypeIfNecessary();
			}

			return metadata;
		}
	}
}
