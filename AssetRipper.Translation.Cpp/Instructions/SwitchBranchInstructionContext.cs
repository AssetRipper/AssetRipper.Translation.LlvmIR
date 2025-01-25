using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class SwitchBranchInstructionContext : BranchInstructionContext
{
	internal SwitchBranchInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length >= 2);
		Debug.Assert(Operands.Length % 2 == 0); // First two operands are the index and the default block. The rest are pairs of case values and blocks.
	}

	public LLVMValueRef IndexOperand => Operands[0];
	public LLVMBasicBlockRef DefaultBlockRef => Operands[1].AsBasicBlock();
	public BasicBlockContext? DefaultBlock => Function?.BasicBlockLookup[DefaultBlockRef];
	public ReadOnlySpan<(LLVMValueRef Case, LLVMValueRef Target)> Cases
	{
		get
		{
			return MemoryMarshal.Cast<LLVMValueRef, (LLVMValueRef Case, LLVMValueRef Target)>(Operands.AsSpan(2));
		}
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		ThrowIfFunctionIsNull();
		Debug.Assert(DefaultBlock is not null);

		LoadOperand(instructions, IndexOperand, out TypeSignature indexTypeSignature);
		CilLocalVariable indexLocal = instructions.AddLocalVariable(indexTypeSignature);
		instructions.Add(CilOpCodes.Stloc, indexLocal);

		CilInstructionLabel[] caseLabels = new CilInstructionLabel[Cases.Length];
		for (int i = 0; i < Cases.Length; i++)
		{
			caseLabels[i] = new();

			instructions.Add(CilOpCodes.Ldloc, indexLocal);
			LoadOperand(instructions, Cases[i].Case);
			instructions.Add(CilOpCodes.Ceq);
			instructions.Add(CilOpCodes.Brtrue, caseLabels[i]);
		}

		CilInstructionLabel defaultLabel = new();
		instructions.Add(CilOpCodes.Br, defaultLabel);

		for (int i = 0; i < Cases.Length; i++)
		{
			BasicBlockContext targetBlock = Function.BasicBlockLookup[Cases[i].Target.AsBasicBlock()];

			caseLabels[i].Instruction = instructions.Add(CilOpCodes.Nop);
			AddLoadIfBranchingToPhi(instructions, targetBlock);
			instructions.Add(CilOpCodes.Br, Function.Labels[targetBlock.Block]);
		}

		// Default case
		{
			defaultLabel.Instruction = instructions.Add(CilOpCodes.Nop);
			AddLoadIfBranchingToPhi(instructions, DefaultBlock);
			instructions.Add(CilOpCodes.Br, Function.Labels[DefaultBlockRef]);
		}
	}
}
