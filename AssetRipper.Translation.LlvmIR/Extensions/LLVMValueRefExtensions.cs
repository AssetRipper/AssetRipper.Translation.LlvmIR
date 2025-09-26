using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class LLVMValueRefExtensions
{
	public static IEnumerable<LLVMValueRef> GetInstructions(this LLVMValueRef value)
	{
		if (value.IsAFunction != default)
		{
			return GetFunctionInstructions(value);
		}
		else if(value.IsABasicBlock != default)
		{
			return value.AsBasicBlock().GetInstructions();
		}
		else if (value.IsAInstruction != default)
		{
			return [value];
		}
		else
		{
			return [];
		}

		static IEnumerable<LLVMValueRef> GetFunctionInstructions(LLVMValueRef function)
		{
			foreach (LLVMBasicBlockRef basicBlock in function.GetBasicBlocks())
			{
				foreach (LLVMValueRef instruction in basicBlock.GetInstructions())
				{
					yield return instruction;
				}
			}
		}
	}

	public static unsafe LLVMValueRef[] GetOperands(this LLVMValueRef value)
	{
		int numOperands = value.OperandCount;
		if (numOperands == 0)
		{
			return [];
		}
		LLVMValueRef[] operands = new LLVMValueRef[numOperands];
		for (int i = 0; i < numOperands; i++)
		{
			operands[i] = value.GetOperand((uint)i);
		}
		return operands;
	}

	public static unsafe LLVMBasicBlockRef[] GetSuccessors(this LLVMValueRef value)
	{
		if (value.IsABasicBlock != null)
		{
			return value.AsBasicBlock().GetSuccessors();
		}

		if (value.IsAInstruction == null)
		{
			return [];
		}

		int numSuccessors = (int)LLVM.GetNumSuccessors(value);
		if (numSuccessors == 0)
		{
			return [];
		}

		LLVMBasicBlockRef[] successors = new LLVMBasicBlockRef[numSuccessors];
		for (int i = 0; i < numSuccessors; i++)
		{
			successors[i] = LLVM.GetSuccessor(value, (uint)i);
		}
		return successors;
	}

	private static unsafe LLVMValueRef GetUser(this LLVMUseRef use) => LLVM.GetUser(use);

	private static unsafe LLVMUseRef GetNextUse(this LLVMUseRef use) => LLVM.GetNextUse(use);

	// This could be used for dead code elimination.
	// For example, when Clang optimization is disabled (the default),
	// arguments always get put into locals even if they're not used.
	public static IEnumerable<LLVMValueRef> GetUsers(this LLVMValueRef value)
	{
		LLVMUseRef use = value.FirstUse;
		while (use.Handle != 0)
		{
			yield return use.GetUser();
			use = use.GetNextUse();
		}
	}

	public static bool IsUsed(this LLVMValueRef value)
	{
		return value.FirstUse != default;
	}

	public static bool IsInstruction(this LLVMValueRef value)
	{
		return value.Kind == LLVMValueKind.LLVMInstructionValueKind;
	}

	public static LLVMOpcode GetOpcode(this LLVMValueRef value)
	{
		return value.IsInstruction() ? value.InstructionOpcode : value.ConstOpcode;
	}

	public static unsafe double GetFloatingPointValue(this LLVMValueRef value)
	{
		int losesInfo = default;
		return value.Kind is LLVMValueKind.LLVMConstantFPValueKind
			? LLVM.ConstRealGetDouble(value, &losesInfo)
			: default;
	}

	public static unsafe LLVMMetadataRef[] GetAllMetadataOtherThanDebugLoc(this LLVMValueRef value)
	{
		if (value.IsAInstruction == null)
		{
			return [];
		}

		nuint metadataCount = 0;
		LLVMValueMetadataEntry* ptr = LLVM.InstructionGetAllMetadataOtherThanDebugLoc(value, &metadataCount);

		LLVMMetadataRef[] metadataArray;
		if (metadataCount == 0)
		{
			metadataArray = [];
			LLVM.DisposeValueMetadataEntries(ptr);
		}
		else
		{
			metadataArray = new LLVMMetadataRef[metadataCount];
			for (uint i = 0; i < metadataCount; i++)
			{
				metadataArray[i] = LLVM.ValueMetadataEntriesGetMetadata(ptr, i);
			}
			LLVM.DisposeValueMetadataEntries(ptr);
		}

		return metadataArray;
	}
}
