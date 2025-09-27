using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class LLVMExtensions
{
	extension(LLVM)
	{
		public static unsafe uint GetEnumAttributeKindForName(ReadOnlySpan<byte> name)
		{
			fixed (byte* pName = name)
			{
				return LLVM.GetEnumAttributeKindForName((sbyte*)pName, (uint)name.Length);
			}
		}
	}
}
