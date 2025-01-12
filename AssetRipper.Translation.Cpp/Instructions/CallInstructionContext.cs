using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CallInstructionContext : InstructionContext
{
	internal CallInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMCall);
		Debug.Assert(Operands.Length >= 1);
	}

	public LLVMValueRef FunctionOperand => Operands[^1];
	public ReadOnlySpan<LLVMValueRef> ArgumentOperands => Operands.AsSpan()[..^1];
	public FunctionContext? FunctionCalled { get; set; }

	public StandAloneSignature MakeStandaloneSignature()
	{
		TypeSignature[] parameterTypes = new TypeSignature[ArgumentOperands.Length];
		for (int i = 0; i < ArgumentOperands.Length; i++)
		{
			LLVMValueRef operand = ArgumentOperands[i];
			parameterTypes[i] = Function.GetOperandTypeSignature(operand);
		}
		MethodSignature methodSignature = MethodSignature.CreateStatic(ResultTypeSignature, parameterTypes);
		return new StandAloneSignature(methodSignature);
	}
}
