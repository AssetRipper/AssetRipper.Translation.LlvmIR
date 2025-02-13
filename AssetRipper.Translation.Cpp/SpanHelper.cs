using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

	public static unsafe T* ToPointer<T>(this ReadOnlySpan<T> span) where T : unmanaged
	{
		return (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
	}
}
