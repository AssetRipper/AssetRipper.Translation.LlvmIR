using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class UnaryMathInstructionContext : InstructionContext
{
	internal UnaryMathInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 1);
		ResultTypeSignature = module.GetTypeSignature(instruction.TypeOf);
	}
	public LLVMValueRef Operand => Operands[0];
	public CilOpCode CilOpCode => Opcode switch
	{
		LLVMOpcode.LLVMFNeg => CilOpCodes.Neg,
		_ => throw new NotSupportedException(),
	};

	public static bool Supported(LLVMOpcode opcode) => opcode is LLVMOpcode.LLVMFNeg;

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		LoadOperand(instructions, Operand);
		instructions.Add(CilOpCode);
		instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
	}
}
