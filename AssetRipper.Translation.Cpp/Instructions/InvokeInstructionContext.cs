using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class InvokeInstructionContext : BaseCallInstructionContext
{
	internal InvokeInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMInvoke);
		Debug.Assert(Operands.Length >= 3);
		Debug.Assert(Operands[^3].IsBasicBlock);
		Debug.Assert(Operands[^2].IsBasicBlock);
	}

	public BasicBlockContext? TryBlockTarget => CatchSwitch?.UltimateTarget;
	public CilInstructionLabel? TryStartLabel => CatchSwitch?.TryStartLabel;
	public CilInstructionLabel? TryEndLabel => CatchSwitch?.TryEndLabel;
	public LLVMBasicBlockRef DefaultBlockRef => Operands[^3].AsBasicBlock();
	public LLVMBasicBlockRef CatchBlockRef => Operands[^2].AsBasicBlock();
	public BasicBlockContext? DefaultBlock => Function?.BasicBlockLookup[DefaultBlockRef];
	public BasicBlockContext? CatchBlock => Function?.BasicBlockLookup[CatchBlockRef];
	public CatchSwitchInstructionContext? CatchSwitch => CatchBlock?.Instructions.FirstOrDefault() as CatchSwitchInstructionContext;

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Debug.Assert(Function is not null);
		Debug.Assert(DefaultBlock is not null);
		Debug.Assert(CatchBlock is not null);
		Debug.Assert(TryBlockTarget is not null);
		Debug.Assert(TryEndLabel is not null);

		base.AddInstructions(instructions);
		if (TryBlockTarget == DefaultBlock)
		{
			instructions.Add(CilOpCodes.Br, TryEndLabel);
		}
		else
		{
			instructions.Add(CilOpCodes.Br, Function.Labels[DefaultBlockRef]);
		}
	}
}
