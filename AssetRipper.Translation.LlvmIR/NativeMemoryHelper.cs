using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR;

internal static unsafe class NativeMemoryHelper
{
	public static void* Allocate(int size)
	{
		return Marshal.AllocHGlobal(size).ToPointer();
	}

	public static void* Allocate(long size)
	{
		return Marshal.AllocHGlobal((nint)size).ToPointer();
	}

	public static void Free(void* ptr)
	{
		Marshal.FreeHGlobal((nint)ptr);
	}

	public static void* Reallocate(void* ptr, int newSize)
	{
		return Marshal.ReAllocHGlobal((nint)ptr, newSize).ToPointer();
	}

	public static void* Reallocate(void* ptr, long newSize)
	{
		return Marshal.ReAllocHGlobal((nint)ptr, (nint)newSize).ToPointer();
	}
}
