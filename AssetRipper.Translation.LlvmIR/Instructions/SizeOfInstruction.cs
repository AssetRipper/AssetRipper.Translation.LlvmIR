using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class SizeOfInstruction(TypeSignature Type) : Instruction
{
	public override int PopCount => 0;
	public override int PushCount => 1;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Sizeof, Type.ToTypeDefOrRef());
	}

	public bool Equals(SizeOfInstruction? other)
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
		return $"SizeOf {{ {Type} }}";
	}
}
