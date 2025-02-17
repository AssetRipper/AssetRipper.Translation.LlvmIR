using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class InvokeInstructionContext : BaseCallInstructionContext
{
	internal InvokeInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMInvoke);
		Debug.Assert(Operands.Length >= 3);
		Debug.Assert(Operands[^3].IsBasicBlock);
		Debug.Assert(Operands[^2].IsBasicBlock);
	}

	public override LLVMValueRef FunctionOperand => Operands[^1];
	public override ReadOnlySpan<LLVMValueRef> ArgumentOperands => Operands.AsSpan()[..^3];
	public LLVMBasicBlockRef DefaultBlockRef => Operands[^3].AsBasicBlock();
	public LLVMBasicBlockRef CatchBlockRef => Operands[^2].AsBasicBlock();
	public BasicBlockContext? DefaultBlock => Function?.BasicBlockLookup[DefaultBlockRef];
	public BasicBlockContext? CatchBlock => Function?.BasicBlockLookup[CatchBlockRef];

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		base.AddInstructions(instructions);

		Debug.Assert(Function is not null);
		Debug.Assert(DefaultBlock is not null);
		instructions.Add(CilOpCodes.Br, Function.Labels[DefaultBlockRef]);
	}
}
