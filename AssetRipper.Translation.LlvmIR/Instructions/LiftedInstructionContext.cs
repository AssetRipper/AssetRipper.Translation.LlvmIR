using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal abstract class LiftedInstructionContext : InstructionContext
{
	protected LiftedInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
	}
}
