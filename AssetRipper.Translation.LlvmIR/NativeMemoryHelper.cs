using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR;

internal static unsafe class NativeMemoryHelper
{
	private static readonly ConcurrentDictionary<nint, long> allocationSizes = new();

	private static void SetAllocation(void* ptr, long size)
	{
		allocationSizes[new(ptr)] = size;
	}

	private static void RemoveAllocation(void* ptr)
	{
		allocationSizes.TryRemove(new(ptr), out _);
	}

	public static void* Allocate(int size)
	{
		return Allocate((long)size);
	}

	public static void* Allocate(long size)
	{
		void* result = NativeMemory.AllocZeroed((nuint)size);
		SetAllocation(result, size);
		return result;
	}

	public static void Free(void* ptr)
	{
		RemoveAllocation(ptr);
		NativeMemory.Free(ptr);
	}

	public static void* Reallocate(void* ptr, int newSize)
	{
		return Reallocate(ptr, (long)newSize);
	}

	public static void* Reallocate(void* ptr, long newSize)
	{
		void* result = NativeMemory.Realloc(ptr, (nuint)newSize);
		SetAllocation(result, newSize);
		if (result != ptr)
		{
			RemoveAllocation(ptr);
		}
		return result;
	}

	public static long Size(void* ptr)
	{
		return allocationSizes[(nint)ptr];
	}
}
