using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class PhiInstructionContext : InstructionContext
{
	internal PhiInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length > 0);
		IncomingBlocks = new BasicBlockContext[Operands.Length];
	}

	public BasicBlockContext[] IncomingBlocks { get; }

	public void InitializeIncomingBlocks()
	{
		for (int i = 0; i < Operands.Length; i++)
		{
			IncomingBlocks[i] = Function.BasicBlockLookup[Instruction.GetIncomingBlock((uint)i)];
		}
	}

	public LLVMValueRef GetOperandForOriginBlock(BasicBlockContext originBlock)
	{
		for (int i = 0; i < Operands.Length; i++)
		{
			if (IncomingBlocks[i] == originBlock)
			{
				return Operands[i];
			}
		}
		throw new InvalidOperationException("The origin block is not among the phi instruction's incoming blocks.");
	}
}
