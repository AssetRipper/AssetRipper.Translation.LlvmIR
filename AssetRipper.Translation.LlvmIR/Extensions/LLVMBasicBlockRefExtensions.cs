using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class LLVMBasicBlockRefExtensions
{
	public static LLVMBasicBlockInstructionsEnumerable GetInstructions(this LLVMBasicBlockRef basicBlock) => basicBlock.Instructions;

	public static unsafe LLVMBasicBlockRef[] GetSuccessors(this LLVMBasicBlockRef value)
	{
		return value.LastInstruction.GetSuccessors();
	}

	public static bool TryGetSingleInstruction(this LLVMBasicBlockRef basicBlock, out LLVMValueRef instruction)
	{
		LLVMValueRef firstInstruction = basicBlock.FirstInstruction;
		if (firstInstruction == default || firstInstruction != basicBlock.LastInstruction)
		{
			instruction = default;
			return false;
		}
		else
		{
			instruction = firstInstruction;
			return true;
		}
	}

	public static bool StartsWithPhi(this LLVMBasicBlockRef basicBlock)
	{
		LLVMValueRef firstInstruction = basicBlock.FirstInstruction;
		return firstInstruction.Handle != 0 && firstInstruction.InstructionOpcode == LLVMOpcode.LLVMPHI;
	}
}
