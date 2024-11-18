using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal sealed class ConditionalBranchInstructionContext : BranchInstructionContext
{
	internal ConditionalBranchInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length == 3);
		Debug.Assert(Operands[0].IsInstruction());
		Debug.Assert(Operands[0] == Instruction.Condition);
		Debug.Assert(Operands[1].IsBasicBlock);
		Debug.Assert(Operands[2].IsBasicBlock);
	}
	public LLVMValueRef Condition => Operands[0];
	// I have no idea why, but the second and third operands seem to be swapped.
	public LLVMBasicBlockRef TrueBlockRef => Operands[2].AsBasicBlock();
	public LLVMBasicBlockRef FalseBlockRef => Operands[1].AsBasicBlock();
	public BasicBlockContext TrueBlock => Function.BasicBlockLookup[TrueBlockRef];
	public BasicBlockContext FalseBlock => Function.BasicBlockLookup[FalseBlockRef];

	public override void AddBranchInstruction()
	{
		CilInstructions.Add(CilOpCodes.Ldloc, Function.InstructionLocals[Operands[0]]);

		if (TargetBlockStartsWithPhi(TrueBlock))
		{
			CilInstructionLabel falseLabel = new();
			CilInstructions.Add(CilOpCodes.Brfalse, falseLabel);

			AddLoadIfBranchingToPhi(TrueBlock);
			CilInstructions.Add(CilOpCodes.Br, Function.Labels[TrueBlockRef]);

			falseLabel.Instruction = CilInstructions.Add(CilOpCodes.Nop);
		}
		else
		{
			CilInstructions.Add(CilOpCodes.Brtrue, Function.Labels[TrueBlockRef]);
		}

		AddLoadIfBranchingToPhi(FalseBlock);
		CilInstructions.Add(CilOpCodes.Br, Function.Labels[FalseBlockRef]);
	}
}
