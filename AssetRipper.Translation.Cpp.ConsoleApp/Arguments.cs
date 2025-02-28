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
}
