namespace AssetRipper.Translation.LlvmIR;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
internal sealed class MangledNameAttribute(string name) : NameAttribute(name)
{
}
