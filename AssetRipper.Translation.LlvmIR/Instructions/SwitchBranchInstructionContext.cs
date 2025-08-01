using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR.Instructions;

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
	private bool IsSequentialAndZeroBased
	{
		get
		{
			for (int i = 0; i < Cases.Length; i++)
			{
				LLVMValueRef caseValue = Cases[i].Case;
				if (caseValue.Kind is not LLVMValueKind.LLVMConstantIntValueKind || caseValue.ConstIntSExt != i)
				{
					return false; // Not a constant integer or not sequential
				}
			}
			return true;
		}
	}

	private BasicBlockContext GetCaseTargetBlock(int index)
	{
		Debug.Assert(Function is not null);
		return Function.BasicBlockLookup[Cases[index].Target.AsBasicBlock()];
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		ThrowIfFunctionIsNull();
		Debug.Assert(DefaultBlock is not null);

		Module.LoadValue(instructions, IndexOperand, out TypeSignature indexTypeSignature);
		if (indexTypeSignature is not CorLibTypeSignature)
		{
			throw new NotSupportedException($"Switch index type '{indexTypeSignature}' is not currently supported.");
		}
		CilLocalVariable indexLocal = instructions.AddLocalVariable(indexTypeSignature);
		instructions.Add(CilOpCodes.Stloc, indexLocal);

		bool[] hasPhi = new bool[Cases.Length];
		CilInstructionLabel[] caseLabels = new CilInstructionLabel[Cases.Length];
		for (int i = 0; i < caseLabels.Length; i++)
		{
			BasicBlockContext targetBlock = GetCaseTargetBlock(i);
			if (TargetBlockStartsWithPhi(targetBlock))
			{
				hasPhi[i] = true;
				caseLabels[i] = new();
			}
			else
			{
				hasPhi[i] = false;
				caseLabels[i] = targetBlock.Label;
			}
		}

		bool defaultHasPhi = TargetBlockStartsWithPhi(DefaultBlock);
		CilInstructionLabel defaultLabel = defaultHasPhi ? new() : DefaultBlock.Label;

		if (IsSequentialAndZeroBased && indexTypeSignature is CorLibTypeSignature { ElementType: ElementType.I4 or ElementType.U4 })
		{
			instructions.Add(CilOpCodes.Ldloc, indexLocal);
			instructions.Add(CilOpCodes.Switch, caseLabels);
		}
		else
		{
			for (int i = 0; i < Cases.Length; i++)
			{
				instructions.Add(CilOpCodes.Ldloc, indexLocal);
				Module.LoadValue(instructions, Cases[i].Case);
				instructions.Add(CilOpCodes.Beq, caseLabels[i]);
			}
		}

		instructions.Add(CilOpCodes.Br, defaultLabel);

		for (int i = 0; i < caseLabels.Length; i++)
		{
			if (hasPhi[i])
			{
				BasicBlockContext targetBlock = GetCaseTargetBlock(i);

				int currentIndex = instructions.Count;
				AddLoadIfBranchingToPhi(instructions, targetBlock);
				instructions.Add(CilOpCodes.Br, targetBlock.Label);
				caseLabels[i].Instruction = instructions[currentIndex];
			}
		}

		if (defaultHasPhi)
		{
			int currentIndex = instructions.Count;
			AddLoadIfBranchingToPhi(instructions, DefaultBlock);
			instructions.Add(CilOpCodes.Br, DefaultBlock.Label);
			defaultLabel.Instruction = instructions[currentIndex];
		}
	}
}
