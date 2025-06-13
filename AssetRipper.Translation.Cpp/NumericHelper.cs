using System.Numerics;
using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.Cpp;

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
}
