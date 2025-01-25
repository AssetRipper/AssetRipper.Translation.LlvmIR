using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class SelectInstructionContext : InstructionContext
{
	internal SelectInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 3);
	}
	public LLVMValueRef ConditionOperand => Operands[0];
	public LLVMValueRef TrueOperand => Operands[1];
	public LLVMValueRef FalseOperand => Operands[2];

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		CilInstructionLabel falseLabel = new();
		CilInstructionLabel endLabel = new();

		Module.LoadValue(instructions, ConditionOperand);
		instructions.Add(CilOpCodes.Brfalse, falseLabel);

		Module.LoadValue(instructions, TrueOperand);
		instructions.Add(CilOpCodes.Br, endLabel);

		falseLabel.Instruction = instructions.Add(CilOpCodes.Nop);
		Module.LoadValue(instructions, FalseOperand);

		endLabel.Instruction = instructions.Add(CilOpCodes.Nop);
		instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
	}
}
