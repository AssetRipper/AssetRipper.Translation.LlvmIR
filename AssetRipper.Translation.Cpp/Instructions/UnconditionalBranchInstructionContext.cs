using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class UnconditionalBranchInstructionContext : BranchInstructionContext
{
	internal UnconditionalBranchInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length == 1);
		Debug.Assert(Operands[0].IsBasicBlock);
	}
	public LLVMBasicBlockRef TargetBlockRef => Operands[0].AsBasicBlock();
	public BasicBlockContext TargetBlock => Function.BasicBlockLookup[TargetBlockRef];

	public override void AddBranchInstruction()
	{
		AddLoadIfBranchingToPhi(TargetBlock);
		CilInstructions.Add(CilOpCodes.Br, Function.Labels[TargetBlockRef]);
	}
}
