using AsmResolver.DotNet.Code.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

/// <summary>
/// A special instruction to indicate the pushing stack effect from the devolution of a phi instruction.
/// </summary>
internal sealed record class PhiPushInstruction : Instruction
{
	public static PhiPushInstruction Instance { get; } = new();
	public override int PopCount => 0;
	public override int PushCount => 1;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
	}
}
