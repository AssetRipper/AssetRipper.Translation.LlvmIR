using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CallInstructionContext : BaseCallInstructionContext
{
	internal CallInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMCall);
		Debug.Assert(Operands.Length >= 1);
	}

	public override LLVMValueRef FunctionOperand => Operands[^1];
	public override ReadOnlySpan<LLVMValueRef> ArgumentOperands => Operands.AsSpan()[..^1];
}
