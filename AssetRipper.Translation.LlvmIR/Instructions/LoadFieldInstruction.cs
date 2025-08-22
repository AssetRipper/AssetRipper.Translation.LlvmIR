using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class LoadFieldInstruction(FieldDefinition Field) : Instruction
{
	public override int PopCount => Field.IsStatic ? 0 : 1;
	public override int PushCount => 1;
	public CilOpCode OpCode => Field.IsStatic ? CilOpCodes.Ldsfld : CilOpCodes.Ldfld;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(OpCode, Field);
	}

	public bool Equals(LoadFieldInstruction? other)
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
