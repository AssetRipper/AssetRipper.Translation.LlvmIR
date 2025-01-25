using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp.Instructions;

internal abstract class BranchInstructionContext : InstructionContext
{
	internal BranchInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		ResultTypeSignature = module.Definition.CorLibTypeFactory.Void;
	}

	protected void AddLoadIfBranchingToPhi(CilInstructionCollection instructions, BasicBlockContext targetBlock)
	{
		if (!TargetBlockStartsWithPhi(targetBlock))
		{
			return;
		}

		ThrowIfBasicBlockIsNull();
		ThrowIfFunctionIsNull();

		foreach (InstructionContext instruction in targetBlock.Instructions)
		{
			if (instruction is PhiInstructionContext phiInstruction)
			{
				LLVMValueRef phiOperand = phiInstruction.GetOperandForOriginBlock(BasicBlock);
				Module.LoadValue(instructions, phiOperand);
				instructions.Add(CilOpCodes.Stloc, phiInstruction.GetLocalVariable());
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
