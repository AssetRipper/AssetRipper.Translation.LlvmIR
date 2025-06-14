namespace AssetRipper.Translation.Cpp;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
internal sealed class MangledNameAttribute(string name) : NameAttribute(name)
{
}
