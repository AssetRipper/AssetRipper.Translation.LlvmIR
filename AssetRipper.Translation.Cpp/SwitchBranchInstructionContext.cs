using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.Cpp;

internal sealed class SwitchBranchInstructionContext : BranchInstructionContext
{
	internal SwitchBranchInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length >= 2);
		Debug.Assert(Operands.Length % 2 == 0); // First two operands are the index and the default block. The rest are pairs of case values and blocks.
	}

	public LLVMValueRef IndexOperand => Operands[0];
	public LLVMBasicBlockRef DefaultBlockRef => Operands[1].AsBasicBlock();
	public BasicBlockContext DefaultBlock => Function.BasicBlockLookup[DefaultBlockRef];
	public ReadOnlySpan<(LLVMValueRef Case, LLVMValueRef Target)> Cases
	{
		get
		{
			return MemoryMarshal.Cast<LLVMValueRef, (LLVMValueRef Case, LLVMValueRef Target)>(Operands.AsSpan(2));
		}
	}

	public override void AddBranchInstruction()
	{
		Function.LoadOperand(IndexOperand, out TypeSignature indexTypeSignature);
		CilLocalVariable indexLocal = CilInstructions.AddLocalVariable(indexTypeSignature);
		CilInstructions.Add(CilOpCodes.Stloc, indexLocal);

		CilInstructionLabel[] caseLabels = new CilInstructionLabel[Cases.Length];
		for (int i = 0; i < Cases.Length; i++)
		{
			caseLabels[i] = new();

			CilInstructions.Add(CilOpCodes.Ldloc, indexLocal);
			Function.LoadOperand(Cases[i].Case);
			CilInstructions.Add(CilOpCodes.Ceq);
			CilInstructions.Add(CilOpCodes.Brtrue, caseLabels[i]);
		}

		CilInstructionLabel defaultLabel = new();
		CilInstructions.Add(CilOpCodes.Br, defaultLabel);

		for (int i = 0; i < Cases.Length; i++)
		{
			BasicBlockContext targetBlock = Function.BasicBlockLookup[Cases[i].Target.AsBasicBlock()];

			caseLabels[i].Instruction = CilInstructions.Add(CilOpCodes.Nop);
			AddLoadIfBranchingToPhi(targetBlock);
			CilInstructions.Add(CilOpCodes.Br, Function.Labels[targetBlock.Block]);
		}

		// Default case
		{
			defaultLabel.Instruction = CilInstructions.Add(CilOpCodes.Nop);
			AddLoadIfBranchingToPhi(DefaultBlock);
			CilInstructions.Add(CilOpCodes.Br, Function.Labels[DefaultBlockRef]);
		}
	}
}
