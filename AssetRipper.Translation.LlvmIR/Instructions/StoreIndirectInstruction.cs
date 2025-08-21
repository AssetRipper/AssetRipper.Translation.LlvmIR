using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class StoreIndirectInstruction(TypeSignature type) : Instruction
{
	public TypeSignature Type { get; } = type;
	public override int PopCount => 2; // Pointer, Value
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.AddStoreIndirect(Type);
	}
}
