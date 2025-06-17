using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR;

public readonly struct AttributeWrapper
{
	private readonly LLVMAttributeRef m_attribute;

	public AttributeWrapper(LLVMAttributeRef attribute)
	{
		m_attribute = attribute;
	}

	public bool IsTypeAttribute => m_attribute.IsTypeAttribute();
	public bool IsStringAttribute => m_attribute.IsStringAttribute();
	public bool IsEnumAttribute => m_attribute.IsEnumAttribute();

	public LLVMTypeRef TypeValue => IsTypeAttribute ? m_attribute.GetTypeAttributeValue() : default;
	public string StringKey => IsStringAttribute ? m_attribute.GetStringAttributeKind() : "";
	public string StringValue => IsStringAttribute ? m_attribute.GetStringAttributeValue() : "";
	public uint EnumKind => IsEnumAttribute ? m_attribute.GetEnumAttributeKind() : default;
	public ulong EnumValue => IsEnumAttribute ? m_attribute.GetEnumAttributeValue() : default;

	public static AttributeWrapper[] FromArray(LLVMAttributeRef[] attributes)
	{
		AttributeWrapper[] wrappers = new AttributeWrapper[attributes.Length];
		for (int i = 0; i < attributes.Length; i++)
		{
			wrappers[i] = new AttributeWrapper(attributes[i]);
		}
		return wrappers;
	}

	public override string ToString()
	{
		if (IsTypeAttribute)
		{
			return $"Type: {TypeValue}";
		}
		if (IsStringAttribute)
		{
			return $"String: {StringKey} = {StringValue}";
		}
		if (IsEnumAttribute)
		{
			return $"Enum: {EnumKind} = {EnumValue}";
		}
		return nameof(AttributeWrapper);
	}
}
