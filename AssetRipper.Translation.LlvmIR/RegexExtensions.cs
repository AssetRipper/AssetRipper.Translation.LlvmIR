using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AssetRipper.Translation.LlvmIR;

internal static class RegexExtensions
{
	public static bool TryMatch(this Regex regex, string input, [NotNullWhen(true)] out Match? match)
	{
		match = regex.Match(input);
		return match.Success;
	}

	public static bool TryMatchAndGetFirstGroup(this Regex regex, string input, [NotNullWhen(true)] out string? groupValue)
	{
		if (regex.TryMatch(input, out Match? match) && match.Groups.Count > 1)
		{
			groupValue = match.Groups[1].Value;
			return true;
		}
		groupValue = null;
		return false;
	}
}
