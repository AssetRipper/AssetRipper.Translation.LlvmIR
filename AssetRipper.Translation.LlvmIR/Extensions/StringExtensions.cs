namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class StringExtensions
{
	public static string? ToNullIfEmpty(this string? value)
	{
		return string.IsNullOrEmpty(value) ? null : value;
	}
}
