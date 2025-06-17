using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal abstract class BranchInstructionContext : InstructionContext
{
	internal BranchInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		ResultTypeSignature = module.Definition.CorLibTypeFactory.Void;
	}
}
