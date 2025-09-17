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

	[CommandLineArgument]
	[Description("The public class name to use in the generated assembly.")]
	public string? ClassName { get; set; }

	[CommandLineArgument("mangled-name")]
	[Description("The set of mangled names.")]
	public string[]? MangledNames { get; set; }

	[CommandLineArgument("new-name")]
	[Description("The set of new names.")]
	public string[]? NewNames { get; set; }

	[CommandLineArgument]
	[Description("The path to the output directory for C# decompilation. If provided, a dll will not be saved.")]
	public string? DecompileDirectory { get; set; }

	[CommandLineArgument]
	[Description("If true, the contents of the decompile directory will be deleted before decompilation starts.")]
	public bool ClearDecompileDirectory { get; set; }

	[CommandLineArgument(DefaultValue = true)]
	[Description("If true, demangled names will be parsed in order to extract additional information.")]
	public bool ParseDemangledSymbols { get; set; }

	[CommandLineArgument(DefaultValue = true)]
	[Description("If true, name attributes will be included in the output.")]
	public bool EmitNameAttributes { get; set; }

	[CommandLineArgument]
	[Description("If true, constant structs and arrays will be initialized from precomputed binary data.")]
	public bool PrecomputeInitializers { get; set; }
}
