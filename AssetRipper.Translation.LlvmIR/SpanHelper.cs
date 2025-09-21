using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR;

internal static class SpanHelper
{
	public static ReadOnlySpan<T> ToReadOnly<T>(this Span<T> span)
	{
		return span;
	}

	public static unsafe T* ToPointer<T>(this ReadOnlySpan<T> span) where T : unmanaged
	{
		return (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
	}

	public static ReadOnlySpan<TTo> Cast<TFrom, TTo>(this ReadOnlySpan<TFrom> span)
		where TFrom : struct
		where TTo : struct
	{
		return MemoryMarshal.Cast<TFrom, TTo>(span);
	}
}
