using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp.Instructions;

internal abstract class LiftedInstructionContext : InstructionContext
{
	protected LiftedInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
	}
}
