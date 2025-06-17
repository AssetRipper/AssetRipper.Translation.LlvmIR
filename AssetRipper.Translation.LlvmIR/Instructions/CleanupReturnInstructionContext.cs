using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

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
		Debug.Assert(Function is not null);
		Debug.Assert(CleanupPad is not null);
		CleanupPad.AddLoad(instructions);
		instructions.Add(CilOpCodes.Stsfld, Module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current)));

		if (UnwindsToCaller)
		{
			instructions.AddDefaultValue(Function.ReturnTypeSignature); // Does nothing if the return type is void
			instructions.Add(CilOpCodes.Ret);
		}
		else
		{
			// Unwind to an exception handler switch or another cleanup pad
			Debug.Assert(TargetBlock is not null);
			AddLoadIfBranchingToPhi(instructions, TargetBlock);
			instructions.Add(CilOpCodes.Br, Function.Labels[TargetBlockRef]);
		}
	}
}
