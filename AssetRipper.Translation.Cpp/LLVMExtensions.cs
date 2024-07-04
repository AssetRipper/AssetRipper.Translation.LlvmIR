using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp;

internal static class LLVMExtensions
{
	public static IEnumerable<LLVMValueRef> GetFunctions(this LLVMModuleRef module)
	{
		LLVMValueRef function = module.FirstFunction;
		while (function.Handle != 0)
		{
			yield return function;
			function = function.NextFunction;
		}
	}

	public static IEnumerable<LLVMValueRef> GetInstructions(this LLVMBasicBlockRef basicBlock)
	{
		LLVMValueRef instruction = basicBlock.FirstInstruction;
		while (instruction.Handle != 0)
		{
			yield return instruction;
			instruction = instruction.NextInstruction;
		}
	}

	public static unsafe LLVMValueRef[] GetOperands(this LLVMValueRef value)
	{
		int numOperands = value.OperandCount;
		if (numOperands == 0)
		{
			return Array.Empty<LLVMValueRef>();
		}
		LLVMValueRef[] operands = new LLVMValueRef[numOperands];
		for (int i = 0; i < numOperands; i++)
		{
			operands[i] = LLVM.GetOperand(value, (uint)i);
		}
		return operands;
	}
}
