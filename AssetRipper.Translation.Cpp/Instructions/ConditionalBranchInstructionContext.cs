using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class ConditionalBranchInstructionContext : BranchInstructionContext
{
	internal ConditionalBranchInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
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
	public BasicBlockContext? TrueBlock => Function?.BasicBlockLookup[TrueBlockRef];
	public BasicBlockContext? FalseBlock => Function?.BasicBlockLookup[FalseBlockRef];

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		LoadOperand(instructions, Condition);

		ThrowIfFunctionIsNull();
		Debug.Assert(TrueBlock is not null);
		Debug.Assert(FalseBlock is not null);

		if (TargetBlockStartsWithPhi(TrueBlock))
		{
			CilInstructionLabel falseLabel = new();
			instructions.Add(CilOpCodes.Brfalse, falseLabel);

			AddLoadIfBranchingToPhi(instructions, TrueBlock);
			instructions.Add(CilOpCodes.Br, Function.Labels[TrueBlockRef]);

			falseLabel.Instruction = instructions.Add(CilOpCodes.Nop);
		}
		else
		{
			instructions.Add(CilOpCodes.Brtrue, Function.Labels[TrueBlockRef]);
		}

		AddLoadIfBranchingToPhi(instructions, FalseBlock);
		instructions.Add(CilOpCodes.Br, Function.Labels[FalseBlockRef]);
	}
}
