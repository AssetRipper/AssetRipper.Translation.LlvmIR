using LLVMSharp.Interop;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR.Extensions;
internal static class LLVMExtensions
{
	public static IEnumerable<LLVMValueRef> GetFunctions(this LLVMModuleRef module)
	{
		LLVMValueRef function = module.FirstFunction;
		while (function.Handle != 0)
		{
			yield return function;
			function = function.NextFunction;
		}
	}

	public static IEnumerable<LLVMValueRef> GetGlobals(this LLVMModuleRef module)
	{
		LLVMValueRef global = module.FirstGlobal;
		while (global.Handle != 0)
		{
			yield return global;
			global = global.NextGlobal;
		}
	}

	private static unsafe LLVMValueRef GetFirstGlobalAlias(this LLVMModuleRef module)
	{
		return LLVM.GetFirstGlobalAlias(module);
	}

	private static unsafe LLVMValueRef GetNextGlobalAlias(this LLVMValueRef alias)
	{
		return LLVM.GetNextGlobalAlias(alias);
	}

	public static IEnumerable<LLVMValueRef> GetGlobalAliases(this LLVMModuleRef module)
	{
		LLVMValueRef alias = module.GetFirstGlobalAlias();
		while (alias.Handle != 0)
		{
			yield return alias;
			alias = alias.GetNextGlobalAlias();
		}
	}

	private static unsafe LLVMValueRef GetFirstGlobalIFunc(this LLVMModuleRef module)
	{
		return LLVM.GetFirstGlobalIFunc(module);
	}

	private static unsafe LLVMValueRef GetNextGlobalIFunc(this LLVMValueRef ifunc)
	{
		return LLVM.GetNextGlobalIFunc(ifunc);
	}

	public static IEnumerable<LLVMValueRef> GetGlobalIFuncs(this LLVMModuleRef module)
	{
		LLVMValueRef ifunc = module.GetFirstGlobalIFunc();
		while (ifunc.Handle != 0)
		{
			yield return ifunc;
			ifunc = ifunc.GetNextGlobalIFunc();
		}
	}

	private static unsafe LLVMNamedMDNodeRef GetFirstNamedMetadata(this LLVMModuleRef module)
	{
		return LLVM.GetFirstNamedMetadata(module);
	}

	private static unsafe LLVMNamedMDNodeRef GetNextNamedMetadata(this LLVMNamedMDNodeRef metadata)
	{
		return LLVM.GetNextNamedMetadata(metadata);
	}

	public static IEnumerable<LLVMNamedMDNodeRef> GetNamedMetadata(this LLVMModuleRef module)
	{
		LLVMNamedMDNodeRef metadata = module.GetFirstNamedMetadata();
		while (metadata.Handle != 0)
		{
			yield return metadata;
			metadata = metadata.GetNextNamedMetadata();
		}
	}

	public static IEnumerable<LLVMValueRef> GetInstructions(this LLVMBasicBlockRef basicBlock)
	{
		LLVMValueRef instruction = basicBlock.FirstInstruction;
		while (instruction.Handle != 0)
		{
			yield return instruction;
			instruction = instruction.NextInstruction;
		}
	}

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

	public static unsafe LLVMBasicBlockRef[] GetSuccessors(this LLVMBasicBlockRef value)
	{
		return value.LastInstruction.GetSuccessors();
	}

	public static bool TryGetSingleInstruction(this LLVMBasicBlockRef basicBlock, out LLVMValueRef instruction)
	{
		LLVMValueRef firstInstruction = basicBlock.FirstInstruction;
		if (firstInstruction == default || firstInstruction != basicBlock.LastInstruction)
		{
			instruction = default;
			return false;
		}
		else
		{
			instruction = firstInstruction;
			return true;
		}
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

	public static unsafe ulong GetABISize(this LLVMTypeRef type, LLVMModuleRef module)
	{
		LLVMTargetDataRef targetData = LLVM.GetModuleDataLayout(module);
		return LLVM.ABISizeOfType(targetData, type);
	}

	public static unsafe string GetStringAttributeKind(this LLVMAttributeRef attribute)
	{
		uint length = 0;
		sbyte* result = LLVM.GetStringAttributeKind(attribute, &length);
		return Marshal.PtrToStringAnsi((IntPtr)result, (int)length);
	}

	public static unsafe string GetStringAttributeValue(this LLVMAttributeRef attribute)
	{
		uint length = 0;
		sbyte* result = LLVM.GetStringAttributeValue(attribute, &length);
		return Marshal.PtrToStringAnsi((IntPtr)result, (int)length);
	}

	public static unsafe LLVMTypeRef GetTypeAttributeValue(this LLVMAttributeRef attribute)
	{
		return LLVM.GetTypeAttributeValue(attribute);
	}

	public static unsafe ulong GetEnumAttributeValue(this LLVMAttributeRef attribute)
	{
		return LLVM.GetEnumAttributeValue(attribute);
	}

	public static unsafe uint GetEnumAttributeKind(this LLVMAttributeRef attribute)
	{
		return LLVM.GetEnumAttributeKind(attribute);
	}

	public static unsafe bool IsTypeAttribute(this LLVMAttributeRef attribute)
	{
		return LLVM.IsTypeAttribute(attribute) != 0;
	}

	public static unsafe bool IsStringAttribute(this LLVMAttributeRef attribute)
	{
		return LLVM.IsStringAttribute(attribute) != 0;
	}

	public static unsafe bool IsEnumAttribute(this LLVMAttributeRef attribute)
	{
		return LLVM.IsEnumAttribute(attribute) != 0;
	}
}
