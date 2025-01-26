namespace AssetRipper.Translation.Cpp;

internal static class SpanHelper
{
	public static ReadOnlySpan<T> ToReadOnly<T>(this Span<T> span)
	{
		return span;
	}

	public static ReadOnlySpan<char> ToCharacterSpan(this string str)
	{
		return str.AsSpan();
	}
}
