﻿using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class CatchSwitchInstructionContext : InstructionContext
{
	internal CatchSwitchInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length >= 1);
	}

	public LLVMValueRef ParentRef => Operands[0];
	public bool HasDefaultUnwind => false;
	public LLVMBasicBlockRef DefaultUnwindTargetRef => HasDefaultUnwind ? Operands[^1].AsBasicBlock() : default;
	public BasicBlockContext? DefaultUnwindTarget => HasDefaultUnwind ? Function?.BasicBlockLookup[DefaultUnwindTargetRef] : null;
	public ReadOnlySpan<LLVMValueRef> Handlers => HasDefaultUnwind ? Operands.AsSpan()[1..^1] : Operands.AsSpan()[1..];
	public IReadOnlyList<CatchPadInstructionContext> CatchPads
	{
		get
		{
			Debug.Assert(Function is not null);
			CatchPadInstructionContext[] catchPads = new CatchPadInstructionContext[Handlers.Length];
			for (int i = 0; i < Handlers.Length; i++)
			{
				BasicBlockContext handlerBlock = Function.BasicBlockLookup[Handlers[i].AsBasicBlock()];
				catchPads[i] = handlerBlock.Instructions.OfType<CatchPadInstructionContext>().First();
			}
			return catchPads;
		}
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Debug.Assert(Function is not null);
		Debug.Assert(Function.PersonalityFunction is not null);
		Debug.Assert(Function.PersonalityFunction.IsIntrinsic, "Personality function should be intrinsic and not have instructions");
		Debug.Assert(Function.PersonalityFunction.ReturnTypeSignature is CorLibTypeSignature { ElementType: ElementType.I4 });
		Debug.Assert(Function.PersonalityFunction.NormalParameters.Length == 0);
		Debug.Assert(Function.PersonalityFunction.IsVariadic);
		Debug.Assert(CatchPads.Count > 0);

		CilInstructionLabel? currentLabel = null;
		for (int i = 0; i < CatchPads.Count; i++)
		{
			CatchPadInstructionContext catchPad = CatchPads[i];
			CilInstructionLabel? previousLabel = currentLabel;
			currentLabel = new();

			int startIndex = instructions.Count;

			CilLocalVariable argumentsInReadOnlySpan = BaseCallInstructionContext.LoadVariadicArguments(instructions, Module, catchPad.Arguments);
			// Call personality function
			instructions.Add(CilOpCodes.Ldloc, argumentsInReadOnlySpan);
			instructions.Add(CilOpCodes.Call, Function.PersonalityFunction.Definition);
			instructions.Add(CilOpCodes.Brtrue, currentLabel);
			AddLoadIfBranchingToPhi(instructions, catchPad.BasicBlock!);
			instructions.Add(CilOpCodes.Br, Function.BasicBlockLookup[catchPad.BasicBlockRef].Label);

			previousLabel?.Instruction = instructions[startIndex];
		}

		int defaultIndex = instructions.Count;

		if (HasDefaultUnwind)
		{
			Debug.Assert(DefaultUnwindTarget is not null);
			AddLoadIfBranchingToPhi(instructions, DefaultUnwindTarget);
			instructions.Add(CilOpCodes.Br, Function.BasicBlockLookup[DefaultUnwindTargetRef].Label);
		}
		else
		{
			// Unwind to caller
			instructions.AddDefaultValue(Function.ReturnTypeSignature);
			instructions.Add(CilOpCodes.Ret);
		}

		currentLabel?.Instruction = instructions[defaultIndex];
	}

	private enum ExceptionDisposition
	{
		/// <summary>
		/// Exception handled; resume execution where it occurred
		/// </summary>
		ContinueExecution,
		/// <summary>
		/// Not handled; search next handler
		/// </summary>
		ContinueSearch,
		/// <summary>
		/// New exception occurred during existing exception handling (unwinding)
		/// </summary>
		NestedException,
		/// <summary>
		/// Unwinding interrupted by another unwind; adjust strategy
		/// </summary>
		/// <remarks>
		/// Can only occur with multi-threading
		/// </remarks>
		CollidedUnwind,
	}
}
