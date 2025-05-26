using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CleanupReturnInstructionContext : InstructionContext
{
	internal CleanupReturnInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length is 1 or 2);
	}

	public LLVMValueRef CleanupPadRef => Operands[0];
	public LLVMBasicBlockRef TargetBlockRef => UnwindsToCaller ? default : Operands[1].AsBasicBlock();
	public CleanupPadInstructionContext? CleanupPad => Function?.InstructionLookup[CleanupPadRef] as CleanupPadInstructionContext;
	public bool UnwindsToCaller => Operands.Length == 1;
	public BasicBlockContext? TargetBlock => UnwindsToCaller ? null : Function?.BasicBlockLookup[TargetBlockRef];

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		if (UnwindsToCaller)
		{
			instructions.Add(CilOpCodes.Rethrow);
		}
		else
		{
			// Unwind to an exception handler switch or another cleanup pad
			Debug.Assert(CleanupPad is not null);
			Debug.Assert(Function is not null);
			instructions.Add(CilOpCodes.Ldloc, CleanupPad.GetLocalVariable());
			instructions.Add(CilOpCodes.Br, Function.Labels[TargetBlockRef]);
		}
	}
}
