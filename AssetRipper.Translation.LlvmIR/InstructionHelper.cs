using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.LlvmIR;

internal static class InstructionHelper
{
	public static TTo BitCast<TFrom, TTo>(TFrom value)
		where TFrom : struct
		where TTo : struct
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(Unsafe.SizeOf<TFrom>(), Unsafe.SizeOf<TTo>(), nameof(TFrom));
		return Unsafe.As<TFrom, TTo>(ref value);
	}

	public static unsafe void VAStart(void** vaListPtr, ReadOnlySpan<nint> args)
	{
		*vaListPtr = args.ToPointer();
	}

	public static unsafe void** VAArg(void*** vaListPtr)
	{
		// This only works on platforms where va_list is just a pointer.

		// Get the current location.
		void** result = *vaListPtr;

		// Move the pointer to the next location.
		*vaListPtr = result + 1;

		return result;
	}

	public static unsafe void* Select(bool condition, void* trueValue, void* falseValue)
	{
		return condition ? trueValue : falseValue;
	}

	public static T Select<T>(bool condition, T trueValue, T falseValue)
	{
		return condition ? trueValue : falseValue;
	}

	public static TValue Select<TCondition, TValue, TValueElement>(TCondition condition, TValue trueValue, TValue falseValue)
		where TCondition : unmanaged, IInlineArray<bool>
		where TValue : unmanaged, IInlineArray<TValueElement>
		where TValueElement : unmanaged
	{
		if (TCondition.Length != TValue.Length)
		{
			throw new ArgumentException($"Condition length ({TCondition.Length}) must match value length ({TValue.Length}).");
		}

		ReadOnlySpan<bool> conditionSpan = condition.AsReadOnlySpan<TCondition, bool>();
		ReadOnlySpan<TValueElement> trueValueSpan = trueValue.AsReadOnlySpan<TValue, TValueElement>();
		ReadOnlySpan<TValueElement> falseValueSpan = falseValue.AsReadOnlySpan<TValue, TValueElement>();

		TValue result = default;
		Span<TValueElement> resultSpan = result.AsSpan<TValue, TValueElement>();

		for (int i = 0; i < TCondition.Length; i++)
		{
			resultSpan[i] = conditionSpan[i] ? trueValueSpan[i] : falseValueSpan[i];
		}

		return result;
	}

	public static TElement ExtractElement<TBuffer, TElement>(this TBuffer buffer, int index)
		where TBuffer : struct, IInlineArray<TElement>
	{
		return buffer.AsReadOnlySpan<TBuffer, TElement>()[index];
	}

	public static TBuffer InsertElement<TBuffer, TElement>(this TBuffer buffer, TElement value, int index)
		where TBuffer : struct, IInlineArray<TElement>
	{
		buffer.AsSpan<TBuffer, TElement>()[index] = value;
		return buffer;
	}

	public static TResult ShuffleVector<TVector, TIndex, TResult, TElement>(TVector vector1, TVector vector2, TIndex indices)
		where TVector : struct, IInlineArray<TElement>
		where TIndex : struct, IInlineArray<int>
		where TResult : struct, IInlineArray<TElement>
		where TElement : unmanaged
	{
		ArgumentOutOfRangeException.ThrowIfNotEqual(TResult.Length, TIndex.Length);

		ReadOnlySpan<TElement> vector1Span = vector1.AsReadOnlySpan<TVector, TElement>();
		ReadOnlySpan<TElement> vector2Span = vector2.AsReadOnlySpan<TVector, TElement>();
		ReadOnlySpan<int> indexSpan = indices.AsReadOnlySpan<TIndex, int>();

		TResult result = default;
		Span<TElement> resultSpan = result.AsSpan<TResult, TElement>();

		for (int i = 0; i < TIndex.Length; i++)
		{
			int index = indexSpan[i];
			ArgumentOutOfRangeException.ThrowIfNegative(index);
			ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, TVector.Length * 2);

			resultSpan[i] = index < TVector.Length ? vector1Span[index] : vector2Span[index - TVector.Length];
		}

		return result;
	}
}
