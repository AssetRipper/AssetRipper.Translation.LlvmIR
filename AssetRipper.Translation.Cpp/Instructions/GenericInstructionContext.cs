using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp.Instructions;

internal class GenericInstructionContext : InstructionContext
{
	public GenericInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		switch (Opcode)
		{
			case LLVMOpcode.LLVMIndirectBr:
				goto default;
			case LLVMOpcode.LLVMInvoke:
				goto default;
			case LLVMOpcode.LLVMUnreachable:
				instructions.Add(CilOpCodes.Ldnull);
				instructions.Add(CilOpCodes.Throw);
				break;
			case LLVMOpcode.LLVMCallBr:
				goto default;
			case LLVMOpcode.LLVMAddrSpaceCast:
				goto default;
			case LLVMOpcode.LLVMUserOp1:
				goto default;
			case LLVMOpcode.LLVMUserOp2:
				goto default;
			case LLVMOpcode.LLVMExtractElement:
				goto default;
			case LLVMOpcode.LLVMInsertElement:
				goto default;
			case LLVMOpcode.LLVMShuffleVector:
				goto default;
			case LLVMOpcode.LLVMExtractValue:
				goto default;
			case LLVMOpcode.LLVMInsertValue:
				goto default;
			case LLVMOpcode.LLVMFreeze:
				goto default;
			case LLVMOpcode.LLVMFence:
				goto default;
			case LLVMOpcode.LLVMAtomicCmpXchg:
				goto default;
			case LLVMOpcode.LLVMAtomicRMW:
				goto default;
			case LLVMOpcode.LLVMResume:
				goto default;
			case LLVMOpcode.LLVMLandingPad:
				goto default;
			case LLVMOpcode.LLVMCleanupRet:
				goto default;
			case LLVMOpcode.LLVMCatchRet:
				goto default;
			case LLVMOpcode.LLVMCatchPad:
				goto default;
			case LLVMOpcode.LLVMCleanupPad:
				goto default;
			case LLVMOpcode.LLVMCatchSwitch:
				goto default;
			default:
				throw new NotSupportedException();
		}
	}
}
