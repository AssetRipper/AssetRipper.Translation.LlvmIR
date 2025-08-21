using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public abstract class ReturnInstruction : Instruction
{
	public static ReturnInstruction Void { get; } = new VoidReturnInstruction();
	public static ReturnInstruction Value { get; } = new ValueReturnInstruction();

	public sealed override bool StackHeightDependent => true;
	public sealed override int PushCount => 0;
	private ReturnInstruction()
	{
	}
	public sealed override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ret);
	}

	private sealed class VoidReturnInstruction : ReturnInstruction
	{
		public override int PopCount => 0;
	}
	private sealed class ValueReturnInstruction : ReturnInstruction
	{
		public override int PopCount => 1;
	}
}
