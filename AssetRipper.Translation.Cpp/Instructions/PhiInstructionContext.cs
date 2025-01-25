using AsmResolver.DotNet.Code.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class PhiInstructionContext : InstructionContext
{
	internal PhiInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length > 0);
		IncomingBlocks = new BasicBlockContext[Operands.Length];
	}

	public BasicBlockContext[] IncomingBlocks { get; }

	public void InitializeIncomingBlocks()
	{
		ThrowIfFunctionIsNull();
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

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		// Handled by branches
	}
}
