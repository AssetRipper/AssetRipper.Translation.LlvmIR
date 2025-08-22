using AsmResolver.DotNet.Code.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

/// <summary>
/// A special instruction to indicate the popping stack effect from the devolution of a phi instruction.
/// </summary>
internal sealed record class PhiPopInstruction : Instruction
{
	public static PhiPopInstruction Instance { get; } = new();
	public override int PopCount => 1;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
	}
}
