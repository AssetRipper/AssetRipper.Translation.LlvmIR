namespace AssetRipper.Translation.LlvmIR.Attributes;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
internal sealed class InlineAssemblyAttribute(string assembly, string constraints) : Attribute
{
	public string Assembly { get; } = assembly;
	public string Constraints { get; } = constraints;
}
