﻿using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class InvokeInstructionContext : BaseCallInstructionContext
{
	internal InvokeInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMInvoke);
		Debug.Assert(Operands.Length >= 3);
		Debug.Assert(Operands[^3].IsBasicBlock);
		Debug.Assert(Operands[^2].IsBasicBlock);
	}

	public LLVMBasicBlockRef DefaultBlockRef => Operands[^3].AsBasicBlock();
	public LLVMBasicBlockRef CatchBlockRef => Operands[^2].AsBasicBlock();
	public BasicBlockContext? DefaultBlock => Function?.BasicBlockLookup[DefaultBlockRef];
	public BasicBlockContext? CatchBlock => Function?.BasicBlockLookup[CatchBlockRef];

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Debug.Assert(Function is not null);
		Debug.Assert(DefaultBlock is not null);
		Debug.Assert(CatchBlock is not null);

		base.AddInstructions(instructions);

		// if exception info is not null, branch to the catch block
		// else branch to the default block

		instructions.Add(CilOpCodes.Ldsfld, Module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current)));

		if (!TargetBlockStartsWithPhi(CatchBlock))
		{
			instructions.Add(CilOpCodes.Brtrue, CatchBlock.Label);

			AddLoadIfBranchingToPhi(instructions, DefaultBlock);
			instructions.Add(CilOpCodes.Br, DefaultBlock.Label);
		}
		else if (!TargetBlockStartsWithPhi(DefaultBlock))
		{
			instructions.Add(CilOpCodes.Brfalse, DefaultBlock.Label);

			AddLoadIfBranchingToPhi(instructions, CatchBlock);
			instructions.Add(CilOpCodes.Br, CatchBlock.Label);
		}
		else
		{
			CilInstructionLabel defaultLabel = new();

			instructions.Add(CilOpCodes.Brfalse, defaultLabel);
			AddLoadIfBranchingToPhi(instructions, CatchBlock);
			instructions.Add(CilOpCodes.Br, CatchBlock.Label);

			int currentIndex = instructions.Count;
			AddLoadIfBranchingToPhi(instructions, DefaultBlock);
			instructions.Add(CilOpCodes.Br, DefaultBlock.Label);
			defaultLabel.Instruction = instructions[currentIndex];
		}
	}
}
