using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR;

internal static class NativeMemoryHelper
{
	public static IntPtr Allocate(int size)
	{
		return Marshal.AllocHGlobal(size);
	}

	public static IntPtr Allocate(long size)
	{
		return Marshal.AllocHGlobal((nint)size);
	}

	public static void Free(IntPtr ptr)
	{
		Marshal.FreeHGlobal(ptr);
	}

	public static IntPtr Reallocate(IntPtr ptr, int newSize)
	{
		return Marshal.ReAllocHGlobal(ptr, newSize);
	}

	public static IntPtr Reallocate(IntPtr ptr, long newSize)
	{
		return Marshal.ReAllocHGlobal(ptr, (nint)newSize);
	}
}
