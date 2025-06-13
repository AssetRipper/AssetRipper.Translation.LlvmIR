using System.Numerics;
using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.Cpp;

internal static partial class NumericHelper
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Add<T>(T x, T y)
		where T : INumberBase<T>
	{
		unchecked
		{
			return x + y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T AddSigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x + y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T AddUnsigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x + y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Subtract<T>(T x, T y)
		where T : INumberBase<T>
	{
		unchecked
		{
			return x - y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T SubtractSigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x - y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T SubtractUnsigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x - y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Multiply<T>(T x, T y)
		where T : INumberBase<T>
	{
		unchecked
		{
			return x * y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T MultiplySigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x * y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T MultiplyUnsigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x * y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Divide<T>(T x, T y)
		where T : INumberBase<T>
	{
		unchecked
		{
			return x / y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T DivideSigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x / y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T DivideUnsigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x / y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Remainder<T>(T x, T y)
		where T : IModulusOperators<T, T, T>
	{
		unchecked
		{
			return x % y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T RemainderSigned<T>(T x, T y)
		where T : IModulusOperators<T, T, T>
	{
		checked
		{
			return x % y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T RemainderUnsigned<T>(T x, T y)
		where T : IModulusOperators<T, T, T>
	{
		checked
		{
			return x % y;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Negate<T>(T x)
		where T : INumberBase<T>
	{
		unchecked
		{
			return -x;
		}
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
