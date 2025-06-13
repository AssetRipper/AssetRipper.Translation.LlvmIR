using System.Numerics;
using System.Numerics.Tensors;

namespace AssetRipper.Translation.Cpp;

internal static class InlineArrayNumericHelper
{
	public static TBuffer Add<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		TensorPrimitives.Add(x.AsReadOnlySpan<TBuffer, TElement>(), y.AsReadOnlySpan<TBuffer, TElement>(), result.AsSpan<TBuffer, TElement>());
		return result;
	}

	public static TBuffer AddSigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.AddSigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer AddUnsigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.AddUnsigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer Subtract<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		TensorPrimitives.Subtract(x.AsReadOnlySpan<TBuffer, TElement>(), y.AsReadOnlySpan<TBuffer, TElement>(), result.AsSpan<TBuffer, TElement>());
		return result;
	}

	public static TBuffer SubtractSigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.SubtractSigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer SubtractUnsigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.SubtractUnsigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer Multiply<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		TensorPrimitives.Multiply(x.AsReadOnlySpan<TBuffer, TElement>(), y.AsReadOnlySpan<TBuffer, TElement>(), result.AsSpan<TBuffer, TElement>());
		return result;
	}

	public static TBuffer MultiplySigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.MultiplySigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer MultiplyUnsigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.MultiplyUnsigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer Divide<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		TensorPrimitives.Divide(x.AsReadOnlySpan<TBuffer, TElement>(), y.AsReadOnlySpan<TBuffer, TElement>(), result.AsSpan<TBuffer, TElement>());
		return result;
	}

	public static TBuffer DivideSigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.DivideSigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer DivideUnsigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.DivideUnsigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer Remainder<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IModulusOperators<TElement, TElement, TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.Remainder(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer RemainderSigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IModulusOperators<TElement, TElement, TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.RemainderSigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer RemainderUnsigned<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IModulusOperators<TElement, TElement, TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.RemainderUnsigned(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer Negate<TBuffer, TElement>(TBuffer x)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.Negate(x.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer ShiftLeft<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IShiftOperators<TElement, int, TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.ShiftLeft(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer ShiftRightLogical<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IShiftOperators<TElement, int, TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.ShiftRightLogical(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer ShiftRightArithmetic<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IShiftOperators<TElement, int, TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.ShiftRightArithmetic(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}

	public static TBuffer BitwiseAnd<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IBitwiseOperators<TElement, TElement, TElement>
	{
		TBuffer result = default;
		TensorPrimitives.BitwiseAnd(x.AsReadOnlySpan<TBuffer, TElement>(), y.AsReadOnlySpan<TBuffer, TElement>(), result.AsSpan<TBuffer, TElement>());
		return result;
	}

	public static TBuffer BitwiseOr<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IBitwiseOperators<TElement, TElement, TElement>
	{
		TBuffer result = default;
		TensorPrimitives.BitwiseOr(x.AsReadOnlySpan<TBuffer, TElement>(), y.AsReadOnlySpan<TBuffer, TElement>(), result.AsSpan<TBuffer, TElement>());
		return result;
	}

	public static TBuffer BitwiseXor<TBuffer, TElement>(TBuffer x, TBuffer y)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IBitwiseOperators<TElement, TElement, TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.BitwiseXor(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}
}
