using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CleanupPadInstructionContext : InstructionContext
{
	internal CleanupPadInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 1);
		ResultTypeSignature = module.Definition.CorLibTypeFactory.Object; // Placeholder for an actual exception type
	}

	public LLVMValueRef Operand => Operands[0];
	public bool IsIndependent => Operand.IsAConstantTokenNone != default;
	public bool IsBlockSelfContained => BasicBlock?.Instructions[^1] is CleanupReturnInstructionContext cleanupReturn && cleanupReturn.UnwindsToCaller;

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
	}
}
