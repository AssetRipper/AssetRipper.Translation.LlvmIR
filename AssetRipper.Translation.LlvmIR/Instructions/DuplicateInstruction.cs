using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class DuplicateInstruction : Instruction
{
	public static DuplicateInstruction Instance { get; } = new();
	public override int PopCount => 1;
	public override int PushCount => 2;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Dup);
	}
}
