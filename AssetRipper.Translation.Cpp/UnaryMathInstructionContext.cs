using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal sealed class UnaryMathInstructionContext : InstructionContext
{
	internal UnaryMathInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length == 1);
		ResultTypeSignature = function.Module.GetTypeSignature(instruction.TypeOf);
	}
	public LLVMValueRef Operand => Operands[0];
	public CilOpCode CilOpCode => Opcode switch
	{
		LLVMOpcode.LLVMFNeg => CilOpCodes.Neg,
		_ => throw new NotSupportedException(),
	};

	public static bool Supported(LLVMOpcode opcode) => opcode is LLVMOpcode.LLVMFNeg;
}
