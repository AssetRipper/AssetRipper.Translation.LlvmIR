using AsmResolver.DotNet.Code.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal abstract class NumericComparisonInstructionContext : InstructionContext
{
	protected NumericComparisonInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length == 2);
		ResultTypeSignature = function.Module.Definition.CorLibTypeFactory.Boolean;
	}
	public LLVMValueRef Operand1 => Operands[0];
	public LLVMValueRef Operand2 => Operands[1];

	public abstract void AddComparisonInstruction(CilInstructionCollection instructions);
}
