namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class StringExtensions
{
	public static string? ToNullIfEmpty(this string? value)
	{
		return string.IsNullOrEmpty(value) ? null : value;
	}

	/// <summary>
	/// Changes the prefix of a string from "get_" or "set_" to "Get_" or "Set_".
	/// </summary>
	/// <remarks>
	/// This prevents collision with the C# property naming convention.
	/// </remarks>
	/// <param name="value">The string to change.</param>
	/// <returns>The new string if necessary. Otherwise, the original string.</returns>
	public static string CapitalizeGetOrSet(this string value)
	{
		if (value.StartsWith("get_", StringComparison.Ordinal))
		{
			return string.Concat("Get_", value.AsSpan(4));
		}
		else if (value.StartsWith("set_", StringComparison.Ordinal))
		{
			return string.Concat("Set_", value.AsSpan(4));
		}
		else
		{
			return value;
		}
	}
}
