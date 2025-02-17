namespace AssetRipper.Translation.Cpp;

internal abstract class NameAttribute(string name) : Attribute
{
	public string Name { get; } = name;
}
