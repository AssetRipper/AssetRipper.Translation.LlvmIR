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
}
