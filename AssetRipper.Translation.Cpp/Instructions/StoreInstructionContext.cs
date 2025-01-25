using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class StoreInstructionContext : InstructionContext
{
	internal StoreInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMStore);
		Debug.Assert(Operands.Length == 2);
		ResultTypeSignature = module.Definition.CorLibTypeFactory.Void;
		StoreTypeSignature = module.GetTypeSignature(StoreType);
	}
	public LLVMValueRef SourceOperand => Operands[0];
	public LLVMValueRef DestinationOperand => Operands[1];
	public InstructionContext? DestinationInstruction { get; set; }
	public LLVMTypeRef StoreType => SourceOperand.TypeOf;
	public TypeSignature StoreTypeSignature { get; set; }

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		if (DestinationInstruction is AllocaInstructionContext { DataLocal: not null } allocaDestination && IsCompatible(allocaDestination))
		{
			if (allocaDestination.DataLocal.VariableType is PointerTypeSignature
				|| SignatureComparer.Default.Equals(allocaDestination.DataLocal.VariableType, StoreTypeSignature))
			{
				LoadValue(instructions, SourceOperand);
				instructions.Add(CilOpCodes.Stloc, allocaDestination.DataLocal);
			}
			else
			{
				instructions.Add(CilOpCodes.Ldloca, allocaDestination.DataLocal);
				LoadValue(instructions, SourceOperand);
				instructions.AddStoreIndirect(StoreTypeSignature);
			}
		}
		else
		{
			LoadValue(instructions, DestinationOperand);
			LoadValue(instructions, SourceOperand);
			instructions.AddStoreIndirect(StoreTypeSignature);
		}

		bool IsCompatible(AllocaInstructionContext allocaDestination)
		{
			Debug.Assert(allocaDestination.DataLocal is not null);

			if (allocaDestination.DataLocal.VariableType is PointerTypeSignature && StoreTypeSignature is PointerTypeSignature)
			{
				return true;
			}

			LLVMModuleRef module = allocaDestination.Module.Module;
			if (allocaDestination.AllocatedType.GetABISize(module) == StoreType.GetABISize(module))
			{
				return true;
			}

			return false;
		}
	}
}
