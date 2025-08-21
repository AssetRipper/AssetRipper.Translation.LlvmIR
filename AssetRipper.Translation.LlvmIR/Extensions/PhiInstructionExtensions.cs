using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class PhiInstructionExtensions
{
	public static LLVMValueRef GetOperandForIncomingBlock(this LLVMValueRef phi, LLVMBasicBlockRef incomingBlock)
	{
		uint count = (uint)phi.OperandCount;
		for (uint i = 0; i < count; i++)
		{
			if (phi.GetIncomingBlock(i) == incomingBlock)
			{
				return phi.GetOperand(i);
			}
		}
		throw new ArgumentException($"The specified incoming block {incomingBlock} is not found in the phi instruction {phi}.");
	}
}
