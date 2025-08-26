using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class StoreIndirectInstruction(TypeSignature Type) : Instruction
{
	public override int PopCount => 2; // Pointer, Value
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.AddStoreIndirect(Type);
	}

	public bool Equals(StoreIndirectInstruction? other)
	{
		if (other is null)
		{
			return false;
		}
		return SignatureComparer.Default.Equals(Type, other.Type);
	}

	public override int GetHashCode()
	{
		return SignatureComparer.Default.GetHashCode(Type);
	}

	protected override string ToStringImplementation()
	{
		return $"StoreIndirect {{ {Type} }}";
	}
}
