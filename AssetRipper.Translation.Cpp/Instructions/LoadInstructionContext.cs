using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class LoadInstructionContext : InstructionContext
{
	internal LoadInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMLoad);
		Debug.Assert(Operands.Length == 1);
	}

	public LLVMValueRef SourceOperand => Operands[0];

	public InstructionContext? SourceInstruction { get; set; }

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		if (SourceInstruction is AllocaInstructionContext { DataLocal: not null } allocaSource && IsCompatible(allocaSource))
		{
			if (allocaSource.DataLocal.VariableType is PointerTypeSignature
				|| SignatureComparer.Default.Equals(allocaSource.DataLocal.VariableType, ResultTypeSignature))
			{
				instructions.Add(CilOpCodes.Ldloc, allocaSource.DataLocal);
			}
			else
			{
				instructions.Add(CilOpCodes.Ldloca, allocaSource.DataLocal);
				instructions.AddLoadIndirect(ResultTypeSignature);
			}
		}
		else
		{
			Module.LoadValue(instructions, SourceOperand);
			instructions.AddLoadIndirect(ResultTypeSignature);
		}

		instructions.Add(CilOpCodes.Stloc, GetLocalVariable());

		bool IsCompatible(AllocaInstructionContext allocaSource)
		{
			Debug.Assert(allocaSource.DataLocal is not null);

			if (allocaSource.DataLocal.VariableType is PointerTypeSignature && ResultTypeSignature is PointerTypeSignature)
			{
				return true;
			}

			LLVMModuleRef module = allocaSource.Module.Module;
			if (allocaSource.AllocatedType.GetABISize(module) == Instruction.TypeOf.GetABISize(module))
			{
				return true;
			}

			return false;
		}
	}
}
