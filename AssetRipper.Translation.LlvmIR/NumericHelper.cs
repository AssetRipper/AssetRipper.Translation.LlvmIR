using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR;

internal static partial class NumericHelper
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Negate<T>(T x)
		where T : IUnaryNegationOperators<T, T>
	{
		return unchecked(-x);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ShiftLeft<T>(T x, T y)
		where T : IShiftOperators<T, int, T>
	{
		return x << ConvertToInt32(y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ShiftRightArithmetic<T>(T x, T y)
		where T : IShiftOperators<T, int, T>
	{
		return x >> ConvertToInt32(y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ShiftRightLogical<T>(T x, T y)
		where T : IShiftOperators<T, int, T>
	{
		return x >>> ConvertToInt32(y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T BitwiseAnd<T>(T x, T y)
		where T : IBitwiseOperators<T, T, T>
	{
		return x & y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T BitwiseOr<T>(T x, T y)
		where T : IBitwiseOperators<T, T, T>
	{
		return x | y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T BitwiseXor<T>(T x, T y)
		where T : IBitwiseOperators<T, T, T>
	{
		return x ^ y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T CtPop<T>(T x) where T : unmanaged
	{
		long count = TensorPrimitives.PopCount(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(ref x)));
		return ConvertFromInt32<T>((int)count);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsOrdered<T>(T x, T y)
		where T : INumberBase<T>
	{
		return !IsUnordered(x, y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsUnordered<T>(T x, T y)
		where T : INumberBase<T>
	{
		return T.IsNaN(x) || T.IsNaN(y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsUnorderedOrEquals<T>(T x, T y)
		where T : INumberBase<T>
	{
		return IsUnordered(x, y) || x == y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsUnorderedOrNotEquals<T>(T x, T y)
		where T : INumberBase<T>
	{
		return IsUnordered(x, y) || x != y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsUnorderedOrGreaterThan<T>(T x, T y)
		where T : INumberBase<T>, IComparisonOperators<T, T, bool>
	{
		return IsUnordered(x, y) || x > y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsUnorderedOrGreaterThanOrEquals<T>(T x, T y)
		where T : INumberBase<T>, IComparisonOperators<T, T, bool>
	{
		return IsUnordered(x, y) || x >= y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsUnorderedOrLessThan<T>(T x, T y)
		where T : INumberBase<T>, IComparisonOperators<T, T, bool>
	{
		return IsUnordered(x, y) || x < y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsUnorderedOrLessThanOrEquals<T>(T x, T y)
		where T : INumberBase<T>, IComparisonOperators<T, T, bool>
	{
		return IsUnordered(x, y) || x <= y;
	}
}
