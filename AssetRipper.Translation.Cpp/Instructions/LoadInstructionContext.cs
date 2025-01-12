using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class LoadInstructionContext : InstructionContext
{
	internal LoadInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMLoad);
		Debug.Assert(Operands.Length == 1);
	}

	public LLVMValueRef SourceOperand => Operands[0];

	public InstructionContext? SourceInstruction { get; set; }
}
