using AsmResolver.DotNet.Code.Cil;
using AssetRipper.Translation.LlvmIR.Variables;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class InitializeInstruction(IVariable variable) : Instruction
{
	public IVariable Variable { get; } = variable;
	public override int PopCount => 0;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Variable.AddStoreDefault(instructions);
	}
}
