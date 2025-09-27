using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class LLVMModuleRefExtensions
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

	/// <summary>
	/// Finds all metadata in the module, referenced from global variables and functions.
	/// </summary>
	/// <param name="module">The module to traverse.</param>
	/// <returns>The distinct metadata items.</returns>
	public static unsafe IEnumerable<LLVMMetadataRef> GetAllMetadata(this LLVMModuleRef module)
	{
		Queue<LLVMMetadataRef> metadataToVisit = new();
		HashSet<LLVMMetadataRef> visitedMetadata = new();
		foreach (LLVMValueRef global in module.GetGlobals())
		{
			metadataToVisit.Enqueue(LibLLVMSharp.GlobalVariableGetGlobalVariableExpression(global));
		}
		foreach (LLVMValueRef function in module.GetFunctions())
		{
			metadataToVisit.Enqueue(LLVM.GetSubprogram(function));
			foreach (LLVMValueRef instruction in function.GetInstructions())
			{
				foreach (LLVMMetadataRef instructionMetadata in instruction.GetAllMetadataOtherThanDebugLoc())
				{
					metadataToVisit.Enqueue(instructionMetadata);
				}
			}
		}
		while (metadataToVisit.TryDequeue(out LLVMMetadataRef metadata))
		{
			if (metadata.Handle == IntPtr.Zero || !visitedMetadata.Add(metadata))
			{
				continue;
			}
			foreach (LLVMMetadataRef operand in metadata.Operands)
			{
				metadataToVisit.Enqueue(operand);
			}
		}

		return visitedMetadata;
	}
}
