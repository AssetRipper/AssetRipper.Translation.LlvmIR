using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CatchPadInstructionContext : InstructionContext
{
	internal CatchPadInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length >= 1);
		Debug.Assert(Operands[^1].IsACatchSwitchInst != default);
	}

	public LLVMValueRef CatchSwitchRef => Operands[^1];
	public CatchSwitchInstructionContext? CatchSwitch => (CatchSwitchInstructionContext?)(Function?.InstructionLookup[CatchSwitchRef]);
	public ReadOnlySpan<LLVMValueRef> Arguments => Operands.AsSpan()[..^1];
	public CatchReturnInstructionContext CatchReturn
	{
		get
		{
			Debug.Assert(Function is not null);
			return Function.Instructions.OfType<CatchReturnInstructionContext>().Single(i => i.CatchPad == this);
		}
	}
	public bool HasFilter => true;
	public CilInstructionLabel HandlerStartLabel { get; } = new();
	public CilInstructionLabel HandlerEndLabel => CatchReturn.Label;
	public CilInstructionLabel FilterStartLabel { get; } = new();
	//public CilInstructionLabel FilterEndLabel { get; } = new();

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		// filter
		if (HasFilter)
		{
			FilterStartLabel.Instruction = instructions.Add(CilOpCodes.Nop);
			instructions.Add(CilOpCodes.Pop);// Pop the exception object
			instructions.Add(CilOpCodes.Ldc_I4_1);// Return true
			instructions.Add(CilOpCodes.Endfilter);
		}

		// handler
		{
			HandlerStartLabel.Instruction = instructions.Add(CilOpCodes.Nop);
			instructions.Add(CilOpCodes.Pop);// Pop the exception object
		}
	}
}
