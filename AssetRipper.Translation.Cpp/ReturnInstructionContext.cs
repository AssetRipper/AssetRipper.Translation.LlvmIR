using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal sealed class ReturnInstructionContext : InstructionContext
{
	internal ReturnInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length is 0 or 1);
	}
	public bool HasReturnValue => Operands.Length == 1;
	public LLVMValueRef ResultOperand => HasReturnValue ? Operands[0] : default;
}
