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
	public LLVMValueRef CatchPadRef => Operands[0];
	public InstructionContext? CatchPad => Function?.InstructionLookup[CatchPadRef];
	public LLVMBasicBlockRef TargetBlockRef => Operands[1].AsBasicBlock();
	public BasicBlockContext? TargetBlock => Function?.BasicBlockLookup[TargetBlockRef];
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		ThrowIfFunctionIsNull();
		Debug.Assert(TargetBlock is not null);

		AddLoadIfBranchingToPhi(instructions, TargetBlock);
		instructions.Add(CilOpCodes.Br, Function.Labels[TargetBlockRef]);
	}
}
