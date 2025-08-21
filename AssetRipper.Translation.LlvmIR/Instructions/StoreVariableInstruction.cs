using AsmResolver.DotNet.Code.Cil;
using AssetRipper.Translation.LlvmIR.Variables;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class StoreVariableInstruction(IVariable variable) : Instruction
{
	public IVariable Variable { get; } = variable;
	public override int PopCount => 1;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Variable.AddStore(instructions);
	}
}
