namespace AssetRipper.Translation.Cpp;

internal interface IHasName
{
	/// <summary>
	/// The name from LLVM.
	/// </summary>
	string MangledName { get; }
	/// <summary>
	/// The demangled name.
	/// </summary>
	string? DemangledName { get; }
	/// <summary>
	/// A clean name that might not be unique.
	/// </summary>
	string CleanName { get; }
	/// <summary>
	/// The unique name used for output.
	/// </summary>
	string Name { get; set; }
}
