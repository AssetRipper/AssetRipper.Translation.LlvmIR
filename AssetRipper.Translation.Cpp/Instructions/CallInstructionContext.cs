using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CallInstructionContext : InstructionContext
{
	internal CallInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMCall);
		Debug.Assert(Operands.Length >= 1);
		Debug.Assert(Operands[^1].Kind == LLVMValueKind.LLVMFunctionValueKind);
	}

	public LLVMValueRef FunctionOperand => Operands[^1];
	public ReadOnlySpan<LLVMValueRef> ArgumentOperands => Operands.AsSpan()[..^1];
	public FunctionContext FunctionCalled { get; set; } = null!; // Set during Analysis
}
