using AsmResolver.DotNet.Code.Cil;
using AssetRipper.Translation.LlvmIR.Variables;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class LoadVariableInstruction(IVariable Variable) : Instruction
{
	public override int PopCount => 0;
	public override int PushCount => 1;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Variable.AddLoad(instructions);
	}
	protected override string ToStringImplementation()
	{
		return $"LoadVariable {{ {Variable} }}";
	}
}
