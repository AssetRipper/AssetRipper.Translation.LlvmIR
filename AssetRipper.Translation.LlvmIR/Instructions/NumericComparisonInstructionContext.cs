using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal abstract class NumericComparisonInstructionContext : InstructionContext
{
	protected NumericComparisonInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 2);
		ResultTypeSignature = module.Definition.CorLibTypeFactory.Boolean;
	}
	public LLVMValueRef Operand1 => Operands[0];
	public LLVMValueRef Operand2 => Operands[1];

	public abstract void AddComparison(CilInstructionCollection instructions);

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Module.LoadValue(instructions, Operand1);
		Module.LoadValue(instructions, Operand2);
		AddComparison(instructions);
		AddStore(instructions);
	}
}
