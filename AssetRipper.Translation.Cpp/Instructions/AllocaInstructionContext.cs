using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class AllocaInstructionContext : InstructionContext
{
	internal AllocaInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMAlloca);
		Debug.Assert(Operands.Length == 1);
		if (Operands[0].Kind is not LLVMValueKind.LLVMConstantIntValueKind)
		{
			throw new NotSupportedException("Variable size alloca not supported");
		}
		AllocatedTypeSignature = module.GetTypeSignature(AllocatedType);
		ResultTypeSignature = AllocatedTypeSignature.MakePointerType();
	}
	public LLVMValueRef SizeOperand => Operands[0];
	public long FixedSize => SizeOperand.ConstIntSExt;
	public unsafe LLVMTypeRef AllocatedType => LLVM.GetAllocatedType(Instruction);
	public TypeSignature AllocatedTypeSignature { get; set; }
	public TypeSignature PointerTypeSignature => ResultTypeSignature;
	public CilLocalVariable? DataLocal { get; set; }
	public CilLocalVariable? PointerLocal { get => ResultLocal; set => ResultLocal = value; }

	public override void CreateLocal(CilInstructionCollection instructions)
	{
		if (FixedSize != 1)
		{
			throw new NotSupportedException("Fixed size array not supported");
		}
		else
		{
			DataLocal = instructions.AddLocalVariable(AllocatedTypeSignature);
		}

		if (Accessors.Count > 0)
		{
			PointerLocal = instructions.AddLocalVariable(PointerTypeSignature);
		}
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		if (DataLocal is null)
		{
			Debug.Assert(PointerLocal is not null);
			throw new NotSupportedException("Stack allocated data not currently supported");
		}
		else if (PointerLocal is not null)
		{
			//Zero out the memory
			instructions.InitializeDefaultValue(DataLocal);

			//Might need slight modifications for fixed size arrays.
			instructions.Add(CilOpCodes.Ldloca, DataLocal);
			instructions.Add(CilOpCodes.Stloc, PointerLocal);
		}
	}
}
