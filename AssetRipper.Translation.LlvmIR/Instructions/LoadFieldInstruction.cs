using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class LoadFieldInstruction(FieldDefinition field) : Instruction
{
	public FieldDefinition Field { get; } = field;
	public override int PopCount => Field.IsStatic ? 0 : 1;
	public override int PushCount => 1;
	public CilOpCode OpCode => Field.IsStatic ? CilOpCodes.Ldsfld : CilOpCodes.Ldfld;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(OpCode, Field);
	}
}
