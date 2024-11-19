using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal sealed class NumericConversionInstructionContext : InstructionContext
{
	internal NumericConversionInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length == 1);
		ResultTypeSignature = (CorLibTypeSignature)function.Module.GetTypeSignature(Instruction.TypeOf);
	}
	public LLVMValueRef Operand => Operands[0];
	public CilOpCode CilOpCode => Opcode switch
	{
		// Sign extend
		LLVMOpcode.LLVMSExt => ResultTypeSignature!.ElementType switch
		{
			ElementType.I1 => CilOpCodes.Conv_I1,
			ElementType.I2 => CilOpCodes.Conv_I2,
			ElementType.I4 => CilOpCodes.Conv_I4,
			ElementType.I8 => CilOpCodes.Conv_I8,
			_ => throw new NotSupportedException(),
		},

		// Zero extend
		LLVMOpcode.LLVMZExt => ResultTypeSignature!.ElementType switch
		{
			ElementType.I1 => CilOpCodes.Conv_U1,
			ElementType.I2 => CilOpCodes.Conv_U2,
			ElementType.I4 => CilOpCodes.Conv_U4,
			ElementType.I8 => CilOpCodes.Conv_U8,
			_ => throw new NotSupportedException(),
		},

		// Truncate
		LLVMOpcode.LLVMTrunc => ResultTypeSignature!.ElementType switch
		{
			ElementType.Boolean => throw new NotSupportedException(),
			ElementType.I1 => CilOpCodes.Conv_I1,
			ElementType.I2 => CilOpCodes.Conv_I2,
			ElementType.I4 => CilOpCodes.Conv_I4,
			ElementType.I8 => CilOpCodes.Conv_I8,
			_ => throw new NotSupportedException(),
		},

		// Floating point
		LLVMOpcode.LLVMFPExt or LLVMOpcode.LLVMFPTrunc => ResultTypeSignature!.ElementType switch
		{
			ElementType.R4 => CilOpCodes.Conv_R4,
			ElementType.R8 => CilOpCodes.Conv_R8,
			_ => throw new NotSupportedException(),
		},

		// Pointer to integer
		LLVMOpcode.LLVMPtrToInt => ResultTypeSignature!.ElementType switch
		{
			ElementType.Boolean => throw new NotSupportedException(),
			ElementType.I1 => CilOpCodes.Conv_U1,
			ElementType.I2 => CilOpCodes.Conv_U2,
			ElementType.I4 => CilOpCodes.Conv_U4,
			ElementType.I8 => CilOpCodes.Conv_U8,
			_ => throw new NotSupportedException(),
		},

		_ => throw new NotSupportedException(),
	};
}
