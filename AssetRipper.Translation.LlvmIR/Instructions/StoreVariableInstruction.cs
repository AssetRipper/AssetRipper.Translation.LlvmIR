using AsmResolver.DotNet.Code.Cil;
using AssetRipper.Translation.LlvmIR.Variables;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class StoreVariableInstruction(IVariable Variable) : Instruction
{
	public override int PopCount => 1;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Variable.AddStore(instructions);
	}
	protected override string ToStringImplementation()
	{
		return $"StoreVariable {{ {Variable} }}";
	}
}
