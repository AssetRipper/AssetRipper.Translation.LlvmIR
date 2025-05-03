using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CatchReturnInstructionContext : BranchInstructionContext
{
	internal CatchReturnInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 2);
		Debug.Assert(Operands[0].IsACatchPadInst != default);
		Debug.Assert(Operands[1].IsBasicBlock);
	}
	public CilInstructionLabel Label { get; } = new();
	public LLVMValueRef CatchPadRef => Operands[0];
	public InstructionContext? CatchPad => Function?.InstructionLookup[CatchPadRef];
	public LLVMBasicBlockRef TargetBlockRef => Operands[1].AsBasicBlock();
	public BasicBlockContext? TargetBlock => Function?.BasicBlockLookup[TargetBlockRef];
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		ThrowIfFunctionIsNull();
		Debug.Assert(TargetBlock is not null);
		
		if (!TargetBlockStartsWithPhi(TargetBlock))
		{
			Label.Instruction = instructions.Add(CilOpCodes.Leave, Function.Labels[TargetBlockRef]);
		}
		else
		{
			CilInstructionLabel leaveTarget = new();
			Label.Instruction = instructions.Add(CilOpCodes.Leave, leaveTarget);
			leaveTarget.Instruction = instructions.Add(CilOpCodes.Nop);
			AddLoadIfBranchingToPhi(instructions, TargetBlock);
			instructions.Add(CilOpCodes.Br, Function.Labels[TargetBlockRef]);
		}
	}
}
