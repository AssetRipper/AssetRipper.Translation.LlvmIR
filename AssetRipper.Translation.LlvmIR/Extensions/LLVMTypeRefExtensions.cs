using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class LLVMTypeRefExtensions
{
	extension (LLVMTypeRef type)
	{
		public void ThrowIfNotInteger()
		{
			if (type.Kind != LLVMTypeKind.LLVMIntegerTypeKind)
			{
				throw new InvalidOperationException($"Expected an integer type, but got {type.Kind}.");
			}
		}

		public void ThrowIfNotCoreLibInteger()
		{
			if (type.Kind != LLVMTypeKind.LLVMIntegerTypeKind || type.IntWidth is not 8 and not 16 and not 32 and not 64)
			{
				throw new InvalidOperationException($"Expected an integer type with a width of 8, 16, 32, or 64 bits, but got {type.Kind} with width {type.IntWidth}.");
			}
		}
	}

	public static unsafe ulong GetABISize(this LLVMTypeRef type, LLVMModuleRef module)
	{
		LLVMTargetDataRef targetData = LLVM.GetModuleDataLayout(module);
		return LLVM.ABISizeOfType(targetData, type);
	}
}
