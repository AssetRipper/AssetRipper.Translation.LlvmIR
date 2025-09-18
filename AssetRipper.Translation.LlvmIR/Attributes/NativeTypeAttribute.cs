namespace AssetRipper.Translation.LlvmIR.Attributes;

/// <summary>
/// The source code type name of the attributed entity.
/// </summary>
[AttributeUsage(AttributeTargets.All)]
internal sealed class NativeTypeAttribute(string name) : NameAttribute(name)
{
}
