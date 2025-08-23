namespace AssetRipper.Translation.LlvmIR.Attributes;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
internal sealed class MangledNameAttribute(string name) : NameAttribute(name)
{
}
