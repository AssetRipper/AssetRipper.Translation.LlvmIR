using System.Numerics;

namespace AssetRipper.Translation.Cpp;

internal static class NumericHelper
{
	public static T Add<T>(T x, T y)
		where T : INumberBase<T>
	{
		unchecked
		{
			return x + y;
		}
	}

	public static T AddSigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x + y;
		}
	}

	public static T AddUnsigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x + y;
		}
	}

	public static T Subtract<T>(T x, T y)
		where T : INumberBase<T>
	{
		unchecked
		{
			return x - y;
		}
	}

	public static T SubtractSigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x - y;
		}
	}

	public static T SubtractUnsigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x - y;
		}
	}

	public static T Multiply<T>(T x, T y)
		where T : INumberBase<T>
	{
		unchecked
		{
			return x * y;
		}
	}

	public static T MultiplySigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x * y;
		}
	}

	public static T MultiplyUnsigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x * y;
		}
	}

	public static T Divide<T>(T x, T y)
		where T : INumberBase<T>
	{
		unchecked
		{
			return x / y;
		}
	}

	public static T DivideSigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x / y;
		}
	}

	public static T DivideUnsigned<T>(T x, T y)
		where T : INumberBase<T>
	{
		checked
		{
			return x / y;
		}
	}

	public static T Remainder<T>(T x, T y)
		where T : IModulusOperators<T, T, T>
	{
		unchecked
		{
			return x % y;
		}
	}

	public static T RemainderSigned<T>(T x, T y)
		where T : IModulusOperators<T, T, T>
	{
		checked
		{
			return x % y;
		}
	}

	public static T RemainderUnsigned<T>(T x, T y)
		where T : IModulusOperators<T, T, T>
	{
		checked
		{
			return x % y;
		}
	}

	public static T Negate<T>(T x)
		where T : INumberBase<T>
	{
		unchecked
		{
			return -x;
		}
	}

	public static T ShiftLeft<T>(T x, T y)
		where T : IShiftOperators<T, T, T>
	{
		return x << y;
	}

	public static T ShiftRightArithmetic<T>(T x, T y)
		where T : IShiftOperators<T, T, T>
	{
		return x >> y;
	}

	public static T ShiftRightLogical<T>(T x, T y)
		where T : IShiftOperators<T, T, T>
	{
		return x >>> y;
	}

	public static T And<T>(T x, T y)
		where T : IBitwiseOperators<T, T, T>
	{
		return x & y;
	}

	public static T Or<T>(T x, T y)
		where T : IBitwiseOperators<T, T, T>
	{
		return x | y;
	}

	public static T Xor<T>(T x, T y)
		where T : IBitwiseOperators<T, T, T>
	{
		return x ^ y;
	}
}
