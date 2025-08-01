using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

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
		DataTypeSignature = FixedSize != 1
			? Module.GetOrCreateInlineArray(AllocatedTypeSignature, (int)FixedSize).Type.ToTypeSignature()
			: AllocatedTypeSignature;
	}
	public LLVMValueRef SizeOperand => Operands[0];
	public long FixedSize => SizeOperand.ConstIntSExt;
	public unsafe LLVMTypeRef AllocatedType => LLVM.GetAllocatedType(Instruction);
	public TypeSignature AllocatedTypeSignature { get; set; }
	public TypeSignature PointerTypeSignature => ResultTypeSignature;
	public TypeSignature DataTypeSignature { get; set; }
	public CilLocalVariable? DataLocal { get; set; }
	public FieldDefinition? DataField { get; set; }
	public CilLocalVariable? PointerLocal { get => ResultLocal; set => ResultLocal = value; }

	public override void CreateLocal(CilInstructionCollection instructions)
	{
		Debug.Assert(Function is not null);

		if (Function.MightThrowAnException)
		{
			Debug.Assert(Function.NeedsStackFrame);
			Debug.Assert(Function.LocalVariablesType is not null);
			DataField = new FieldDefinition($"Instruction_{Index}", FieldAttributes.Public, DataTypeSignature);
			Function.LocalVariablesType.Fields.Add(DataField);
		}
		else
		{
			DataLocal = instructions.AddLocalVariable(DataTypeSignature);
		}

		if (Accessors.Count > 0)
		{
			PointerLocal = instructions.AddLocalVariable(PointerTypeSignature);
		}
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		if (PointerLocal is null)
		{
			return;
		}

		if (DataLocal is not null)
		{
			//Zero out the memory
			instructions.InitializeDefaultValue(DataLocal);

			//Store the address of the data in the pointer local
			instructions.Add(CilOpCodes.Ldloca, DataLocal);
			instructions.Add(CilOpCodes.Stloc, PointerLocal);
		}
		else
		{
			Debug.Assert(Function is not null);
			Debug.Assert(DataField is not null);

			//Store the address of the data in the pointer local
			Function.AddLocalVariablesPointer(instructions);
			instructions.Add(CilOpCodes.Ldflda, DataField);
			instructions.Add(CilOpCodes.Conv_U);
			instructions.Add(CilOpCodes.Stloc, PointerLocal);
		}
	}
}
