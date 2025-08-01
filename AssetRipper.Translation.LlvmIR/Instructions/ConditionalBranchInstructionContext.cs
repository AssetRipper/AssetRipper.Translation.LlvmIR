﻿using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class ConditionalBranchInstructionContext : BranchInstructionContext
{
	internal ConditionalBranchInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 3);
		Debug.Assert(Operands[0].IsInstruction() || Operands[0].IsConstant);
		Debug.Assert(Operands[0] == Instruction.Condition);
		Debug.Assert(Operands[1].IsBasicBlock);
		Debug.Assert(Operands[2].IsBasicBlock);
	}
	public LLVMValueRef Condition => Operands[0];
	// I have no idea why, but the second and third operands seem to be swapped.
	public LLVMBasicBlockRef TrueBlockRef => Operands[2].AsBasicBlock();
	public LLVMBasicBlockRef FalseBlockRef => Operands[1].AsBasicBlock();
	public BasicBlockContext? TrueBlock => Function?.BasicBlockLookup[TrueBlockRef];
	public BasicBlockContext? FalseBlock => Function?.BasicBlockLookup[FalseBlockRef];

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Module.LoadValue(instructions, Condition);

		ThrowIfFunctionIsNull();
		Debug.Assert(TrueBlock is not null);
		Debug.Assert(FalseBlock is not null);

		if (!TargetBlockStartsWithPhi(TrueBlock))
		{
			// The true block does not start with a phi instruction.
			// The false block might or might not start with a phi instruction.

			instructions.Add(CilOpCodes.Brtrue, TrueBlock.Label);

			AddLoadIfBranchingToPhi(instructions, FalseBlock);
			instructions.Add(CilOpCodes.Br, FalseBlock.Label);
		}
		else if (!TargetBlockStartsWithPhi(FalseBlock))
		{
			// Only the true block starts with a phi instruction.

			instructions.Add(CilOpCodes.Brfalse, FalseBlock.Label);

			AddLoadIfBranchingToPhi(instructions, TrueBlock);
			instructions.Add(CilOpCodes.Br, TrueBlock.Label);
		}
		else
		{
			// Both target blocks start with a phi instruction.

			CilInstructionLabel falseLabel = new();
			instructions.Add(CilOpCodes.Brfalse, falseLabel);

			AddLoadIfBranchingToPhi(instructions, TrueBlock);
			instructions.Add(CilOpCodes.Br, Function.BasicBlockLookup[TrueBlockRef].Label);

			int falseIndex = instructions.Count;
			AddLoadIfBranchingToPhi(instructions, FalseBlock);
			instructions.Add(CilOpCodes.Br, Function.BasicBlockLookup[FalseBlockRef].Label);

			falseLabel.Instruction = instructions[falseIndex];
		}
	}
}
