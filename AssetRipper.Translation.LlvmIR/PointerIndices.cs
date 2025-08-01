using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.LlvmIR;

internal static unsafe class PointerIndices
{
	private static readonly Dictionary<int, IntPtr> IndexToPointer = new();
	private static readonly Dictionary<IntPtr, int> PointerToIndex = new();

	public static void* Register(void* ptr)
	{
		ThrowIfNull(ptr);

		int index = IndexToPointer.Count + 1; // Start from 1

		IndexToPointer.Add(index, (IntPtr)ptr);
		PointerToIndex.Add((IntPtr)ptr, index);

		return ptr;

		static void ThrowIfNull(void* value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value is null)
			{
				throw new ArgumentNullException(paramName);
			}
		}
	}

	public static int GetIndex(void* ptr)
	{
		if (ptr is null)
		{
			return 0;
		}

		return PointerToIndex[(IntPtr)ptr];
	}

	public static void* GetPointer(int index)
	{
		if (index == 0)
		{
			return null;
		}

		return (void*)IndexToPointer[index];
	}
}
