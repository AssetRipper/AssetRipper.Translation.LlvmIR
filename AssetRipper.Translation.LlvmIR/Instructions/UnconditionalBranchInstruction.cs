using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class UnconditionalBranchInstruction(BasicBlock target) : Instruction
{
	public BasicBlock Target { get; } = target;
	public override bool StackHeightDependent => true;
	public override int PopCount => 0;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Br, Target.Label);
	}
}
