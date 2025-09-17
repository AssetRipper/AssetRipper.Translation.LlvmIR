using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR;

internal static unsafe class NativeMemoryHelper
{
	private static readonly ConcurrentDictionary<nint, long> allocationSizes = new();

	private static void SetAllocation(nint ptr, long size)
	{
		allocationSizes[ptr] = size;
	}

	private static void RemoveAllocation(nint ptr)
	{
		allocationSizes.TryRemove(ptr, out _);
	}

	public static void* Allocate(int size)
	{
		return Allocate((long)size);
	}

	public static void* Allocate(long size)
	{
		nint result = Marshal.AllocHGlobal((nint)size);
		SetAllocation(result, size);
		return result.ToPointer();
	}

	public static void Free(void* ptr)
	{
		nint value = (nint)ptr;
		RemoveAllocation(value);
		Marshal.FreeHGlobal(value);
	}

	public static void* Reallocate(void* ptr, int newSize)
	{
		return Reallocate(ptr, (long)newSize);
	}

	public static void* Reallocate(void* ptr, long newSize)
	{
		nint value = (nint)ptr;
		nint result = Marshal.ReAllocHGlobal(value, (nint)newSize);
		SetAllocation(result, newSize);
		if (result != value)
		{
			RemoveAllocation(value);
		}
		return result.ToPointer();
	}

	public static long Size(void* ptr)
	{
		return allocationSizes[(nint)ptr];
	}
}
