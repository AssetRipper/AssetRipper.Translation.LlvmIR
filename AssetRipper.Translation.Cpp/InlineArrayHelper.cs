using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.Cpp;

internal static class InlineArrayHelper
{
	public static Span<TElement> InlineArrayAsSpan<TBuffer, TElement>(ref TBuffer buffer, int length)
	{
		return MemoryMarshal.CreateSpan(ref Unsafe.As<TBuffer, TElement>(ref buffer), length);
	}

	public static ReadOnlySpan<TElement> InlineArrayAsReadOnlySpan<TBuffer, TElement>(ref TBuffer buffer, int length)
	{
		return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<TBuffer, TElement>(ref buffer), length);
	}

	public static void SetInlineArray<TBuffer, TElement>(ref TBuffer buffer, int length, ReadOnlySpan<TElement> span)
	{
		span.CopyTo(InlineArrayAsSpan<TBuffer, TElement>(ref buffer, length));
	}

	public static TBuffer Create<TBuffer, TElement>(ReadOnlySpan<TElement> contents) where TBuffer : struct
	{
		TBuffer buffer = default;
		SetInlineArray(ref buffer, contents.Length, contents);
		return buffer;
	}

	public static ReadOnlySpan<T> ToReadOnly<T>(this Span<T> span)
	{
		return span;
	}
}
