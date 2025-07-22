using Ookii.CommandLine;
using System.ComponentModel;

namespace LargeProjectCompiler;

[GeneratedParser]
[ParseOptions(IsPosix = true)]
internal sealed partial class Arguments
{
	[CommandLineArgument(IsPositional = true)]
	[Description("Path to the compile_commands.json file.")]
	public string CompileCommandsPath { get; set; } = "";

	[CommandLineArgument]
	[Description("Output path for the final .bc file.")]
	public string? Output { get; set; }

	[CommandLineArgument]
	[Description("Path to llvm-link")]
	public string? LlvmLink { get; set; }

	[CommandLineArgument("exclude")]
	[Description("Paths starting with one of these strings will be excluded from linking.")]
	public string[]? ExcludePrefixes { get; set; }

	public bool ShouldInclude(string path)
	{
		if (ExcludePrefixes is not null)
		{
			foreach (string prefix in ExcludePrefixes)
			{
				if (path.StartsWith(prefix, StringComparison.Ordinal))
				{
					return false;
				}
			}
		}
		return true;
	}
}
