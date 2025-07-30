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

	/// <summary>
	/// If true, demangled names will be parsed in order to extract additional information.
	/// </summary>
	public bool ParseDemangledSymbols { get; set; }

	/// <summary>
	/// If true, name attributes will be included in the output.
	/// </summary>
	public bool EmitNameAttributes { get; set; }

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
