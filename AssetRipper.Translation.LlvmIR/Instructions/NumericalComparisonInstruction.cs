namespace AssetRipper.Translation.LlvmIR.Instructions;

public abstract record class NumericalComparisonInstruction : Instruction
{
	public override int PopCount => 2;
	public override int PushCount => 1;
	private protected NumericalComparisonInstruction()
	{
	}
}
