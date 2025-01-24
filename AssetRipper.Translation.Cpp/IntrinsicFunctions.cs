using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.Cpp;

#pragma warning disable IDE0060 // Remove unused parameter
internal static partial class IntrinsicFunctions
{
	public static class Implemented
	{
	}
	public static class Unimplemented
	{
	}

	public unsafe static void llvm_memcpy_p0_p0_i32(void* destination, void* source, int length, bool isVolatile)
	{
		Unsafe.CopyBlock(destination, source, (uint)length);
	}

	public unsafe static void llvm_memcpy_p0_p0_i64(void* destination, void* source, long length, bool isVolatile)
	{
		Unsafe.CopyBlock(destination, source, (uint)length);
	}

	public unsafe static void llvm_memmove_p0_p0_i32(void* destination, void* source, int length, bool isVolatile)
	{
		// Same as memcpy, except that the source and destination are allowed to overlap.
		byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
		Span<byte> span = new(buffer, 0, length);
		new ReadOnlySpan<byte>(source, length).CopyTo(span);
		span.CopyTo(new Span<byte>(destination, length));
		ArrayPool<byte>.Shared.Return(buffer);
	}

	public unsafe static void llvm_memmove_p0_p0_i64(void* destination, void* source, long length, bool isVolatile)
	{
		llvm_memmove_p0_p0_i32(destination, source, (int)length, isVolatile);
	}

	public unsafe static void llvm_memset_p0_i32(void* destination, sbyte value, int length, bool isVolatile)
	{
		new Span<byte>(destination, length).Fill(unchecked((byte)value));
	}

	public unsafe static void llvm_memset_p0_i64(void* destination, sbyte value, long length, bool isVolatile)
	{
		llvm_memset_p0_i32(destination, value, (int)length, isVolatile);
	}

	public unsafe static void* malloc(long size)
	{
		return (void*)Marshal.AllocHGlobal((int)size);
	}

	public unsafe static void free(void* ptr)
	{
		Marshal.FreeHGlobal((IntPtr)ptr);
	}

	public static int llvm_abs_i32(int value, bool throwIfMinValue) => llvm_abs(value, throwIfMinValue);

	public static long llvm_abs_i64(long value, bool throwIfMinValue) => llvm_abs(value, throwIfMinValue);

	private static T llvm_abs<T>(T value, bool throwIfMinValue) where T : unmanaged, INumber<T>, IMinMaxValue<T>
	{
		return T.Abs(value);
	}

	public static float llvm_fmuladd_f32(float P_0, float P_1, float P_2)
	{
		return P_0 * P_1 + P_2;
	}

	public static double llvm_fmuladd_f64(double P_0, double P_1, double P_2)
	{
		return P_0 * P_1 + P_2;
	}
}
#pragma warning restore IDE0060 // Remove unused parameter
