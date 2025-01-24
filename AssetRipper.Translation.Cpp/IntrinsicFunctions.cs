using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.Cpp;

#pragma warning disable IDE0060 // Remove unused parameter
internal static class IntrinsicFunctions
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
}
#pragma warning restore IDE0060 // Remove unused parameter
