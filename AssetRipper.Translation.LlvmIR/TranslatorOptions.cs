namespace AssetRipper.Translation.LlvmIR;

public sealed record class TranslatorOptions
{
	/// <summary>
	/// If <see langword="true"/>, references to System.Private.CoreLib will be replaced with System.Runtime.
	/// </summary>
	public bool FixAssemblyReferences { get; set; } = false;

	/// <summary>
	/// The root namespace to use for the generated assembly.
	/// </summary>
	public string? Namespace { get; set; }

	/// <summary>
	/// The module name to use for the generated assembly.
	/// </summary>
	public string? ModuleName { get; set; }

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
