using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class BinaryMathInstructionContext : InstructionContext
{
	internal BinaryMathInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 2);
	}
	public LLVMValueRef Operand1 => Operands[0];
	public LLVMValueRef Operand2 => Operands[1];
	public CilOpCode CilOpCode => Opcode switch
	{
		LLVMOpcode.LLVMAdd or LLVMOpcode.LLVMFAdd => CilOpCodes.Add,
		LLVMOpcode.LLVMSub or LLVMOpcode.LLVMFSub => CilOpCodes.Sub,
		LLVMOpcode.LLVMMul or LLVMOpcode.LLVMFMul => CilOpCodes.Mul,
		LLVMOpcode.LLVMSDiv or LLVMOpcode.LLVMFDiv => CilOpCodes.Div,
		LLVMOpcode.LLVMSRem or LLVMOpcode.LLVMFRem => CilOpCodes.Rem,
		LLVMOpcode.LLVMUDiv => CilOpCodes.Div_Un,
		LLVMOpcode.LLVMURem => CilOpCodes.Rem_Un,
		LLVMOpcode.LLVMShl => CilOpCodes.Shl,
		LLVMOpcode.LLVMLShr => CilOpCodes.Shr_Un,//Logical
		LLVMOpcode.LLVMAShr => CilOpCodes.Shr,//Arithmetic
		LLVMOpcode.LLVMAnd => CilOpCodes.And,
		LLVMOpcode.LLVMOr => CilOpCodes.Or,
		LLVMOpcode.LLVMXor => CilOpCodes.Xor,
		_ => throw new NotSupportedException(),
	};

	public static bool Supported(LLVMOpcode opcode) => opcode switch
	{
		LLVMOpcode.LLVMAdd or LLVMOpcode.LLVMFAdd or
		LLVMOpcode.LLVMSub or LLVMOpcode.LLVMFSub or
		LLVMOpcode.LLVMMul or LLVMOpcode.LLVMFMul or
		LLVMOpcode.LLVMSDiv or LLVMOpcode.LLVMFDiv or
		LLVMOpcode.LLVMSRem or LLVMOpcode.LLVMFRem or
		LLVMOpcode.LLVMUDiv or LLVMOpcode.LLVMURem or
		LLVMOpcode.LLVMShl or LLVMOpcode.LLVMLShr or LLVMOpcode.LLVMAShr or
		LLVMOpcode.LLVMAnd or LLVMOpcode.LLVMOr or LLVMOpcode.LLVMXor => true,
		_ => false,
	};

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		LoadValue(instructions, Operand1);
		LoadValue(instructions, Operand2);
		instructions.Add(CilOpCode);
		instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
	}
}
