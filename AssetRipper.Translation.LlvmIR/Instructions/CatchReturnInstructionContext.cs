using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class CatchReturnInstructionContext : BranchInstructionContext
{
	internal CatchReturnInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 2);
		Debug.Assert(Operands[0].IsACatchPadInst != default);
		Debug.Assert(Operands[1].IsBasicBlock);
	}
	public LLVMValueRef CatchPadRef => Operands[0];
	public InstructionContext? CatchPad => Function?.InstructionLookup[CatchPadRef];
	public LLVMBasicBlockRef TargetBlockRef => Operands[1].AsBasicBlock();
	public BasicBlockContext? TargetBlock => Function?.BasicBlockLookup[TargetBlockRef];
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Debug.Assert(Function is not null);
		Debug.Assert(CatchPad is not null);
		Debug.Assert(TargetBlock is not null);

		CatchPad.AddLoad(instructions);
		instructions.Add(CilOpCodes.Callvirt, Module.InjectedTypes[typeof(ExceptionInfo)].Methods.Single(m => m.Name == nameof(ExceptionInfo.Dispose) && m.IsPublic));

		AddLoadIfBranchingToPhi(instructions, TargetBlock);
		instructions.Add(CilOpCodes.Br, Function.Labels[TargetBlockRef]);
	}
}
