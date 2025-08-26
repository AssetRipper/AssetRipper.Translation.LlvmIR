using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed record class ReturnDefaultInstruction(TypeSignature Type) : Instruction
{
	public override bool StackHeightDependent => true;
	public override int PopCount => 0;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.AddDefaultValue(Type); // Does nothing if the return type is void
		instructions.Add(CilOpCodes.Ret);
	}

	public bool Equals(ReturnDefaultInstruction? other)
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
		return $"ReturnDefault {{ {Type} }}";
	}
}
