using Ookii.CommandLine;
using System.ComponentModel;

namespace AssetRipper.Translation.Cpp.ConsoleApp;

[GeneratedParser]
[ParseOptions(IsPosix = true)]
sealed partial class Arguments
{
	[CommandLineArgument(IsPositional = true)]
	[Description("The path to the input LLVM IR text file.")]
	public required string Input { get; set; }

	[CommandLineArgument("mangled-name")]
	[Description("The set of mangled names.")]
	public string[]? MangledNames { get; set; }

	[CommandLineArgument("new-name")]
	[Description("The set of new names.")]
	public string[]? NewNames { get; set; }
}
