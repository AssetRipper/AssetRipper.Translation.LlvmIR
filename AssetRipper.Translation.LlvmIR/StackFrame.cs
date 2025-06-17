using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct StackFrame
{
	internal readonly int Index;
	private void* Locals;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private StackFrame(int index, int size)
	{
		Index = index;
		Locals = size > 0
			? unchecked((void*)Marshal.AllocHGlobal(size))
			: null;
	}

	internal void FreeLocals()
	{
		if (Locals != null)
		{
			Marshal.FreeHGlobal(unchecked((IntPtr)Locals));
			Locals = null;
		}
	}

	public readonly ref T GetLocalsRef<T>() where T : unmanaged
	{
		return ref Unsafe.AsRef<T>(Locals);
	}

	public readonly T* GetLocalsPointer<T>() where T : unmanaged
	{
		return (T*)Locals;
	}

	internal static StackFrame Create<T>(int index) where T : unmanaged
	{
		return new StackFrame(index, sizeof(T));
	}
}
