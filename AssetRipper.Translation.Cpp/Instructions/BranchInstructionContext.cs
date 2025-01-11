using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp.Instructions;

internal abstract class BranchInstructionContext : InstructionContext
{
	internal BranchInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		ResultTypeSignature = function.Module.Definition.CorLibTypeFactory.Void;
	}

	public abstract void AddBranchInstruction();

	protected void AddLoadIfBranchingToPhi(BasicBlockContext targetBlock)
	{
		if (!TargetBlockStartsWithPhi(targetBlock))
		{
			return;
		}

		foreach (InstructionContext instruction in targetBlock.Instructions)
		{
			if (instruction is PhiInstructionContext phiInstruction)
			{
				LLVMValueRef phiOperand = phiInstruction.GetOperandForOriginBlock(Block);
				Function.LoadOperand(phiOperand);
				CilInstructions.Add(CilOpCodes.Stloc, Function.InstructionLocals[phiInstruction.Instruction]);
			}
			else
			{
				break;
			}
		}
	}

	protected static bool TargetBlockStartsWithPhi(BasicBlockContext targetBlock)
	{
		return targetBlock.Instructions.Count > 0 && targetBlock.Instructions[0] is PhiInstructionContext;
	}
}
