namespace AssetRipper.Translation.LlvmIR.Attributes;

/// <summary>
/// Indicates that an intrinsic method might "throw" an emulated exception.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class MightThrowAttribute : Attribute
{
}
