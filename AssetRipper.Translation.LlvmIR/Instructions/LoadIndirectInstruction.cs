using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class LoadIndirectInstruction(TypeSignature type) : Instruction
{
	public TypeSignature Type { get; } = type;
	public override int PopCount => 1; // Pointer
	public override int PushCount => 1; // Value
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.AddLoadIndirect(Type);
	}
}
