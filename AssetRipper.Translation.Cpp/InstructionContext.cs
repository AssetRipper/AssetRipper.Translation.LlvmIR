using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal sealed class InstructionContext
{
	public InstructionContext(LLVMValueRef instruction, FunctionContext function)
	{
		Instruction = instruction;
		Function = function;
		Operands = instruction.GetOperands();
	}

	public LLVMValueRef Instruction { get; }
	public CilInstructionCollection CilInstructions => Function.Instructions;
	public FunctionContext Function { get; }
	public LLVMValueRef[] Operands { get; }

	public TypeSignature GetOperandTypeSignature(int index)
	{
		return Function.GetOperandTypeSignature(Operands[index]);
	}

	public void LoadOperand(int index, out TypeSignature typeSignature)
	{
		Function.LoadOperand(Operands[index], out typeSignature);
	}

	public void LoadOperand(int index)
	{
		Function.LoadOperand(Operands[index]);
	}

	public void AddBinaryMathInstruction(CilOpCode opCode)
	{
		Debug.Assert(Operands.Length == 2);
		Function.LoadOperand(Operands[0], out TypeSignature resultTypeSignature);
		Function.LoadOperand(Operands[1]);
		CilLocalVariable resultLocal = CilInstructions.AddLocalVariable(resultTypeSignature);
		CilInstructions.Add(opCode);
		CilInstructions.Add(CilOpCodes.Stloc, resultLocal);
		Function.InstructionLocals[Instruction] = resultLocal;
	}

	public void AddUnaryMathInstruction(CilOpCode opCode)
	{
		Debug.Assert(Operands.Length == 1);
		Function.LoadOperand(Operands[0], out TypeSignature resultTypeSignature);
		CilLocalVariable resultLocal = CilInstructions.AddLocalVariable(resultTypeSignature);
		CilInstructions.Add(opCode);
		CilInstructions.Add(CilOpCodes.Stloc, resultLocal);
		Function.InstructionLocals[Instruction] = resultLocal;
	}

	public void AddIntegerComparisonInstruction()
	{
		Debug.Assert(Operands.Length == 2);
		Function.LoadOperand(Operands[0]);
		Function.LoadOperand(Operands[1]);
		CilLocalVariable resultLocal = CilInstructions.AddLocalVariable(Function.Module.Definition.CorLibTypeFactory.Boolean);
		switch (Instruction.ICmpPredicate)
		{
			case LLVMIntPredicate.LLVMIntEQ:
				CilInstructions.Add(CilOpCodes.Ceq);
				break;
			case LLVMIntPredicate.LLVMIntNE:
				CilInstructions.Add(CilOpCodes.Ceq);
				CilInstructions.AddBooleanNot();
				break;
			case LLVMIntPredicate.LLVMIntUGT:
				CilInstructions.Add(CilOpCodes.Cgt_Un);
				break;
			case LLVMIntPredicate.LLVMIntUGE:
				CilInstructions.Add(CilOpCodes.Clt_Un);
				CilInstructions.AddBooleanNot();
				break;
			case LLVMIntPredicate.LLVMIntULT:
				CilInstructions.Add(CilOpCodes.Clt_Un);
				break;
			case LLVMIntPredicate.LLVMIntULE:
				CilInstructions.Add(CilOpCodes.Cgt_Un);
				CilInstructions.AddBooleanNot();
				break;
			case LLVMIntPredicate.LLVMIntSGT:
				CilInstructions.Add(CilOpCodes.Cgt);
				break;
			case LLVMIntPredicate.LLVMIntSGE:
				CilInstructions.Add(CilOpCodes.Clt);
				CilInstructions.AddBooleanNot();
				break;
			case LLVMIntPredicate.LLVMIntSLT:
				CilInstructions.Add(CilOpCodes.Clt);
				break;
			case LLVMIntPredicate.LLVMIntSLE:
				CilInstructions.Add(CilOpCodes.Cgt);
				CilInstructions.AddBooleanNot();
				break;
			default:
				throw new InvalidOperationException($"Unknown comparison predicate: {Instruction.ICmpPredicate}");
		};
		CilInstructions.Add(CilOpCodes.Stloc, resultLocal);
		Function.InstructionLocals[Instruction] = resultLocal;
	}

	public void AddFloatComparisonInstruction()
	{
		Debug.Assert(Operands.Length == 2);
		Function.LoadOperand(Operands[0]);
		Function.LoadOperand(Operands[1]);
		CilLocalVariable resultLocal = CilInstructions.AddLocalVariable(Function.Module.Definition.CorLibTypeFactory.Boolean);
		switch (Instruction.FCmpPredicate)
		{
			case LLVMRealPredicate.LLVMRealOEQ:
			case LLVMRealPredicate.LLVMRealUEQ:
				CilInstructions.Add(CilOpCodes.Ceq);
				break;
			case LLVMRealPredicate.LLVMRealONE:
			case LLVMRealPredicate.LLVMRealUNE:
				CilInstructions.Add(CilOpCodes.Ceq);
				CilInstructions.AddBooleanNot();
				break;
			case LLVMRealPredicate.LLVMRealUGT:
				CilInstructions.Add(CilOpCodes.Cgt_Un);
				break;
			case LLVMRealPredicate.LLVMRealUGE:
				CilInstructions.Add(CilOpCodes.Clt_Un);
				CilInstructions.AddBooleanNot();
				break;
			case LLVMRealPredicate.LLVMRealULT:
				CilInstructions.Add(CilOpCodes.Clt_Un);
				break;
			case LLVMRealPredicate.LLVMRealULE:
				CilInstructions.Add(CilOpCodes.Cgt_Un);
				CilInstructions.AddBooleanNot();
				break;
			case LLVMRealPredicate.LLVMRealOGT:
				CilInstructions.Add(CilOpCodes.Cgt);
				break;
			case LLVMRealPredicate.LLVMRealOGE:
				CilInstructions.Add(CilOpCodes.Clt);
				CilInstructions.AddBooleanNot();
				break;
			case LLVMRealPredicate.LLVMRealOLT:
				CilInstructions.Add(CilOpCodes.Clt);
				break;
			case LLVMRealPredicate.LLVMRealOLE:
				CilInstructions.Add(CilOpCodes.Cgt);
				CilInstructions.AddBooleanNot();
				break;
			default:
				throw new NotImplementedException($"Unknown comparison predicate: {Instruction.ICmpPredicate}");
		};
		CilInstructions.Add(CilOpCodes.Stloc, resultLocal);
		Function.InstructionLocals[Instruction] = resultLocal;
	}

	public void AddBranchInstruction()
	{
		if (Instruction.IsConditional)
		{
			Debug.Assert(Operands.Length == 3);
			Debug.Assert(Operands[0] == Instruction.Condition);
			Debug.Assert(Operands[1].IsBasicBlock);
			Debug.Assert(Operands[2].IsBasicBlock);


			CilInstructions.Add(CilOpCodes.Ldloc, Function.InstructionLocals[Operands[0]]);

			// I have no idea why, but the second and third operands seem to be swapped.
			LLVMBasicBlockRef trueBlock = Operands[2].AsBasicBlock();
			LLVMBasicBlockRef falseBlock = Operands[1].AsBasicBlock();

			if (TargetBlockStartsWithPhi(trueBlock, out LLVMValueRef truePhiInstruction))
			{
				CilInstructionLabel falseLabel = new();
				CilInstructions.Add(CilOpCodes.Brfalse, falseLabel);

				AddLoadIfBranchingToPhi(trueBlock, Instruction.InstructionParent);
				CilInstructions.Add(CilOpCodes.Br, Function.Labels[trueBlock]);

				falseLabel.Instruction = CilInstructions.Add(CilOpCodes.Nop);
			}
			else
			{
				CilInstructions.Add(CilOpCodes.Brtrue, Function.Labels[trueBlock]);
			}

			AddLoadIfBranchingToPhi(falseBlock, Instruction.InstructionParent);
			CilInstructions.Add(CilOpCodes.Br, Function.Labels[falseBlock]);
		}
		else
		{
			Debug.Assert(Operands.Length == 1);
			Debug.Assert(Operands[0].IsBasicBlock);
			LLVMBasicBlockRef targetBlock = Operands[0].AsBasicBlock();
			AddLoadIfBranchingToPhi(targetBlock, Instruction.InstructionParent);
			CilInstructions.Add(CilOpCodes.Br, Function.Labels[targetBlock]);
		}

		static bool TargetBlockStartsWithPhi(LLVMBasicBlockRef targetBlock, out LLVMValueRef phiInstruction)
		{
			phiInstruction = targetBlock.FirstInstruction;
			return phiInstruction.InstructionOpcode is LLVMOpcode.LLVMPHI;
		}

		void AddLoadIfBranchingToPhi(LLVMBasicBlockRef targetBlock, LLVMBasicBlockRef originBlock)
		{
			if (!TargetBlockStartsWithPhi(targetBlock, out LLVMValueRef phiInstruction))
			{
				return;
			}

			LLVMValueRef[] phiOperands = phiInstruction.GetOperands();
			for (int i = 0; i < phiOperands.Length; i++)
			{
				LLVMBasicBlockRef sourceBlock = phiInstruction.GetIncomingBlock((uint)i);
				if (sourceBlock == originBlock)
				{
					Function.LoadOperand(phiOperands[i]);
					return;
				}
			}
			throw new InvalidOperationException("The origin block is not among the phi instruction's incoming blocks.");
		}
	}
}
