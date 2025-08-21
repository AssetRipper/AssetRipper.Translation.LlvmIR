using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class StoreFieldInstruction(FieldDefinition field) : Instruction
{
	public FieldDefinition Field { get; } = field;
	public override int PopCount => Field.IsStatic ? 1 : 2;
	public override int PushCount => 0;
	public CilOpCode OpCode => Field.IsStatic ? CilOpCodes.Stsfld : CilOpCodes.Stfld;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(OpCode, Field);
	}
}
