using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class InitializeObjectInstruction(TypeSignature Type) : Instruction
{
	public override int PopCount => 1;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Initobj, Type.ToTypeDefOrRef());
	}
}
