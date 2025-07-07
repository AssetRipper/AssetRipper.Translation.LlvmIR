using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class UnconditionalBranchInstructionContext : BranchInstructionContext
{
	internal UnconditionalBranchInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 1);
		Debug.Assert(Operands[0].IsBasicBlock);
	}
	public LLVMBasicBlockRef TargetBlockRef => Operands[0].AsBasicBlock();
	public BasicBlockContext? TargetBlock => Function?.BasicBlockLookup[TargetBlockRef];

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		ThrowIfFunctionIsNull();
		Debug.Assert(TargetBlock is not null);

		AddLoadIfBranchingToPhi(instructions, TargetBlock);
		instructions.Add(CilOpCodes.Br, Function.BasicBlockLookup[TargetBlockRef].Label);
	}
}
