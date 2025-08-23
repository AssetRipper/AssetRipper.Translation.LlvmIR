namespace AssetRipper.Translation.LlvmIR.Attributes;

internal abstract class NameAttribute(string name) : Attribute
{
	public string Name { get; } = name;
}
