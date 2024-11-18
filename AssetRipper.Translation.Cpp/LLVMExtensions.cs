using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp;
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

	public static unsafe LLVMValueRef[] GetOperands(this LLVMValueRef value)
	{
		int numOperands = value.OperandCount;
		if (numOperands == 0)
		{
			return Array.Empty<LLVMValueRef>();
		}
		LLVMValueRef[] operands = new LLVMValueRef[numOperands];
		for (int i = 0; i < numOperands; i++)
		{
			operands[i] = value.GetOperand((uint)i);
		}
		return operands;
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
}
