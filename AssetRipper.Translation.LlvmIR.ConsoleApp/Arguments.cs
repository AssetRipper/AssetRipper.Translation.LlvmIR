using Ookii.CommandLine;
using System.ComponentModel;

namespace AssetRipper.Translation.LlvmIR.ConsoleApp;

[GeneratedParser]
[ParseOptions(IsPosix = true)]
sealed partial class Arguments
{
	[CommandLineArgument(IsPositional = true)]
	[Description("The path to the input LLVM IR text file.")]
	public required string Input { get; set; }

	[CommandLineArgument]
	[Description("The root namespace to use for the generated assembly.")]
	public string? Namespace { get; set; }

	[CommandLineArgument]
	[Description("The module name to use for the generated assembly.")]
	public string? ModuleName { get; set; }

	[CommandLineArgument("mangled-name")]
	[Description("The set of mangled names.")]
	public string[]? MangledNames { get; set; }

	[CommandLineArgument("new-name")]
	[Description("The set of new names.")]
	public string[]? NewNames { get; set; }

	[CommandLineArgument]
	[Description("The path to the output directory for C# decompilation. If provided, a dll will not be saved.")]
	public string? DecompileDirectory { get; set; }
}
