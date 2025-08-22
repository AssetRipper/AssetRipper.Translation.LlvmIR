using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class SwitchInstruction(TypeSignature IndexType, (long Value, BasicBlock Target)[] Cases) : Instruction
{
	public override bool StackHeightDependent => true;
	public override int PopCount => 1; // Value
	public override int PushCount => 0;
	private bool IsSequentialAndZeroBased
	{
		get
		{
			for (int i = 0; i < Cases.Length; i++)
			{
				if (Cases[i].Value != i)
				{
					return false; // Not sequential or not zero-based
				}
			}
			return true; // All cases are sequential and zero-based
		}
	}

	private BasicBlock GetCaseTargetBlock(int index)
	{
		return Cases[index].Target;
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		bool isInt64 = IndexType is CorLibTypeSignature { ElementType: ElementType.I8 or ElementType.U8 };
		if (IsSequentialAndZeroBased && !isInt64)
		{
			CilInstructionLabel[] caseLabels = new CilInstructionLabel[Cases.Length];
			for (int i = 0; i < caseLabels.Length; i++)
			{
				BasicBlock targetBlock = GetCaseTargetBlock(i);
				caseLabels[i] = targetBlock.Label;
			}

			instructions.Add(CilOpCodes.Switch, caseLabels);
		}
		else if (isInt64)
		{
			CilLocalVariable indexLocal = instructions.AddLocalVariable(IndexType);
			instructions.Add(CilOpCodes.Stloc, indexLocal);

			for (int i = 0; i < Cases.Length; i++)
			{
				instructions.Add(CilOpCodes.Ldloc, indexLocal);
				instructions.Add(CilOpCodes.Ldc_I8, Cases[i].Value);
				instructions.Add(CilOpCodes.Beq, Cases[i].Target.Label);
			}
		}
		else
		{
			CilLocalVariable indexLocal = instructions.AddLocalVariable(IndexType);
			instructions.Add(CilOpCodes.Stloc, indexLocal);

			for (int i = 0; i < Cases.Length; i++)
			{
				instructions.Add(CilOpCodes.Ldloc, indexLocal);
				instructions.Add(CilOpCodes.Ldc_I4, (int)Cases[i].Value);
				instructions.Add(CilOpCodes.Beq, Cases[i].Target.Label);
			}
		}
	}

	public bool Equals(SwitchInstruction? other)
	{
		if (other is null)
		{
			return false;
		}
		if (ReferenceEquals(this, other))
		{
			return true;
		}
		if (!SignatureComparer.Default.Equals(IndexType, other.IndexType) || Cases.Length != other.Cases.Length)
		{
			return false;
		}
		for (int i = 0; i < Cases.Length; i++)
		{
			if (Cases[i].Value != other.Cases[i].Value || !Cases[i].Target.Equals(other.Cases[i].Target))
			{
				return false;
			}
		}
		return true;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(SignatureComparer.Default.GetHashCode(IndexType), Cases.Length);
	}
}
