using System.Text.Json.Serialization;

namespace LargeProjectCompiler;

internal sealed record class CompileCommand
{
	[JsonPropertyName("directory")]
	public string Directory { get; set; } = "";

	[JsonPropertyName("command")]
	public string Command { get; set; } = "";

	[JsonPropertyName("file")]
	public string File { get; set; } = "";

	[JsonPropertyName("output")]
	public string Output { get; set; } = "";
}
