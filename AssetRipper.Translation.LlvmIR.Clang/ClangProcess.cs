using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AssetRipper.Translation.LlvmIR.Clang;

public static partial class ClangProcess
{
	private static string? helpString;
	private static Version? version;
	private static string? versionString;

	public static string HelpString => helpString ??= GetStringFromClang("--help");

	public static Version Version
	{
		get
		{
			if (version is null)
			{
				GetClangVersionInfo();
			}
			return version;
		}
	}

	public static string VersionString
	{
		get
		{
			if (versionString is null)
			{
				GetClangVersionInfo();
			}
			return versionString;
		}
	}

	[MemberNotNull(nameof(version), nameof(versionString))]
	private static void GetClangVersionInfo()
	{
		string output = GetStringFromClang("--version");
		Match match = VersionRegex().Match(output);
		if (!match.Success)
		{
			throw new InvalidOperationException("Failed to parse clang version");
		}
		versionString = match.Groups["version"].Value;
		version = new Version(versionString);
	}

	private static string GetStringFromClang(string arguments)
	{
		ProcessStartInfo processInfo = new("clang", arguments)
		{
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		using (Process process = new())
		{
			process.StartInfo = processInfo;
			process.Start();
			process.WaitForExit();
			return process.StandardOutput.ReadToEnd();
		}
	}

	[GeneratedRegex(@"clang version (?<version>\d+\.\d+\.\d+)")]
	private static partial Regex VersionRegex();
}
