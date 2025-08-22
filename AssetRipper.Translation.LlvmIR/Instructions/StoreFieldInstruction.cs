using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class StoreFieldInstruction(FieldDefinition Field) : Instruction
{
	public override int PopCount => Field.IsStatic ? 1 : 2;
	public override int PushCount => 0;
	public CilOpCode OpCode => Field.IsStatic ? CilOpCodes.Stsfld : CilOpCodes.Stfld;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(OpCode, Field);
	}

	public bool Equals(StoreFieldInstruction? other)
	{
		if (other is null)
		{
			return false;
		}
		return SignatureComparer.Default.Equals(Field, other.Field);
	}

	public override int GetHashCode()
	{
		return SignatureComparer.Default.GetHashCode(Field);
	}
}
