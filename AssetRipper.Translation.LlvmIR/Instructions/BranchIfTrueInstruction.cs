using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class BranchIfTrueInstruction(BasicBlock target) : Instruction
{
	public BasicBlock Target { get; } = target;
	public override bool StackHeightDependent => true;
	public override int PopCount => 1; // Condition
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Brtrue, Target.Label);
	}
}
