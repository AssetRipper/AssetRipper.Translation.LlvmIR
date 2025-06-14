namespace AssetRipper.Translation.Cpp;

public sealed record class TranslatorOptions
{
	/// <summary>
	/// If <see langword="true"/>, references to System.Private.CoreLib will be replaced with System.Runtime.
	/// </summary>
	public bool FixAssemblyReferences { get; set; } = false;

	public Dictionary<string, string> RenamedSymbols { get; init; } = new();
}
