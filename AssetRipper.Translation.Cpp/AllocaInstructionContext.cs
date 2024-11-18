using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal sealed class AllocaInstructionContext : InstructionContext
{
	internal AllocaInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMAlloca);
		Debug.Assert(Operands.Length == 1);
		if (Operands[0].Kind is not LLVMValueKind.LLVMConstantIntValueKind)
		{
			throw new NotSupportedException("Variable size alloca not supported");
		}
	}
	public LLVMValueRef SizeOperand => Operands[0];
	public long FixedSize => SizeOperand.ConstIntSExt;
	public unsafe LLVMTypeRef AllocatedType => LLVM.GetAllocatedType(Instruction);
	public TypeSignature AllocatedTypeSignature { get; set; } = null!; // Set during Analysis
	public TypeSignature PointerTypeSignature => ResultTypeSignature!; // Set during Analysis
	public CilLocalVariable? DataLocal { get; set; }
	public CilLocalVariable? PointerLocal { get; set; } // Might be removable
	public void InitializePointerTypeSignature()
	{
		ResultTypeSignature = AllocatedTypeSignature.MakePointerType();
	}
}
