using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class SelectInstructionContext : InstructionContext
{
	internal SelectInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length == 3);
	}
	public LLVMValueRef ConditionOperand => Operands[0];
	public LLVMValueRef TrueOperand => Operands[1];
	public LLVMValueRef FalseOperand => Operands[2];
}
