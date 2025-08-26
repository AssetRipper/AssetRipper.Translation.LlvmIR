using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class LoadIndirectInstruction(TypeSignature Type) : Instruction
{
	public override int PopCount => 1; // Pointer
	public override int PushCount => 1; // Value
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.AddLoadIndirect(Type);
	}

	public bool Equals(LoadIndirectInstruction? other)
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
		return $"LoadIndirect {{ {Type} }}";
	}
}
