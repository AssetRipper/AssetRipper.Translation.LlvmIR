using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.Cpp;

internal static class InstructionHelper
{
	public static TTo BitCast<TFrom, TTo>(TFrom value)
		where TFrom : struct
		where TTo : struct
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(Unsafe.SizeOf<TFrom>(), Unsafe.SizeOf<TTo>(), nameof(TFrom));
		return Unsafe.As<TFrom, TTo>(ref value);
	}
}
