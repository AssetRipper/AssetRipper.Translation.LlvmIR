using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp.Instructions;

internal class GenericInstructionContext(LLVMValueRef instruction, ModuleContext module) : InstructionContext(instruction, module)
{
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		switch (Opcode)
		{
			case LLVMOpcode.LLVMIndirectBr:
				goto default;
			case LLVMOpcode.LLVMUnreachable:
				AddThrowNull(instructions);
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
				instructions.Add(CilOpCodes.Rethrow);
				break;
			case LLVMOpcode.LLVMLandingPad:
				goto default;
			default:
				AddInstructionNotSupported(instructions);
				break;
		}
	}

	private static void AddThrowNull(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldnull);
		instructions.Add(CilOpCodes.Throw);
	}

	private void AddInstructionNotSupported(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldstr, Opcode.ToString());
		instructions.Add(CilOpCodes.Ldstr, Instruction.ToString().Trim());
		if (!HasResult)
		{
			MethodDefinition method = Module.InstructionNotSupportedExceptionType.GetMethodByName(nameof(InstructionNotSupportedException.Throw));
			instructions.Add(CilOpCodes.Call, method);
		}
		else if (ResultTypeSignature is PointerTypeSignature)
		{
			MethodDefinition method = Module.InstructionNotSupportedExceptionType.GetMethodByName(nameof(InstructionNotSupportedException.ThrowPointer));
			instructions.Add(CilOpCodes.Call, method);
			instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
		}
		else
		{
			MethodDefinition method = Module.InstructionNotSupportedExceptionType.GetMethodByName(nameof(InstructionNotSupportedException.ThrowStruct));
			instructions.Add(CilOpCodes.Call, method.MakeGenericInstanceMethod(ResultTypeSignature));
			instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
		}
	}
}
