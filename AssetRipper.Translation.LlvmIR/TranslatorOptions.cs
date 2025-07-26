namespace AssetRipper.Translation.LlvmIR;

public sealed record class TranslatorOptions
{
	/// <summary>
	/// The root namespace to use for the generated assembly.
	/// </summary>
	public string? Namespace { get; set; }

	/// <summary>
	/// The module name to use for the generated assembly.
	/// </summary>
	public string? ModuleName { get; set; }

	/// <summary>
	/// The public class name to use in the generated assembly.
	/// </summary>
	public string? ClassName { get; set; }

	public Dictionary<string, string> RenamedSymbols { get; init; } = new();

	public string? GetNamespace(string? subNamespace)
	{
		if (string.IsNullOrEmpty(Namespace))
		{
			return string.IsNullOrEmpty(subNamespace) ? null : subNamespace;
		}
		else if (string.IsNullOrEmpty(subNamespace))
		{
			return Namespace;
		}
		else
		{
			return $"{Namespace}.{subNamespace}";
		}
	}
}
