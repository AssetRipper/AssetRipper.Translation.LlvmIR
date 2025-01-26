using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.Extensions;
using AssetRipper.Translation.Cpp.Instructions;
using LLVMSharp.Interop;
using System.Runtime.InteropServices;
using System.Text;

namespace AssetRipper.Translation.Cpp;

public static unsafe class CppTranslator
{
	static CppTranslator()
	{
		Patches.Apply();
	}

	public static ModuleDefinition Translate(string name, string content, bool fixAssemblyReferences = false)
	{
		return Translate(name, Encoding.UTF8.GetBytes(content), fixAssemblyReferences);
	}

	public static ModuleDefinition Translate(string name, ReadOnlySpan<byte> content, bool fixAssemblyReferences = false)
	{
		fixed (byte* ptr = content)
		{
			nint namePtr = Marshal.StringToHGlobalAnsi(name);
			LLVMMemoryBufferRef buffer = LLVM.CreateMemoryBufferWithMemoryRange((sbyte*)ptr, (nuint)content.Length, (sbyte*)namePtr, 1);
			try
			{
				LLVMContextRef context = LLVMContextRef.Create();
				try
				{
					LLVMModuleRef module = context.ParseIR(buffer);
					return Translate(module, fixAssemblyReferences);
				}
				finally
				{
					context.Dispose();
				}
			}
			finally
			{
				// This fails randomly with no real explanation.
				// I'm fairly certain that the IR text data is only referenced (not copied),
				// so the memory leak of not disposing the buffer is probably not a big deal.
				//LLVM.DisposeMemoryBuffer(buffer);

				Marshal.FreeHGlobal(namePtr);
			}
		}
	}

	private static ModuleDefinition Translate(LLVMModuleRef module, bool fixAssemblyReferences)
	{
		bool removeDeadCode = false;
		if (removeDeadCode)
		{
			//Need to expose these in libLLVMSharp

			//Old passes:
			//https://github.com/llvm/llvm-project/blob/f02eae7487992ecddc335530b55126b15e388b62/llvm/include/llvm-c/Transforms/Scalar.h

			//New passes:
			//https://github.com/llvm/llvm-project/blob/6e4bb60adef6abd34516f9121930eaa84e41e04a/llvm/include/llvm/LinkAllPasses.h

			using LLVMPassManagerRef passManager = LLVM.CreatePassManager();
			LLVMPassBuilderOptionsRef builderOptions = LLVM.CreatePassBuilderOptions();
			passManager.Run(module);
		}

		{
			var globals = module.GetGlobals().ToList();
			var aliases = module.GetGlobalAliases().ToList();
			var ifuncs = module.GetGlobalIFuncs().ToList();
			var metadataList = module.GetNamedMetadata().ToList();
		}

		ModuleDefinition moduleDefinition = new("ConvertedCpp", KnownCorLibs.SystemRuntime_v9_0_0_0);

		moduleDefinition.AddTargetFrameworkAttributeForDotNet9();

		ModuleContext moduleContext = new(module, moduleDefinition);

		foreach (LLVMValueRef global in module.GetGlobals())
		{
			GlobalVariableContext globalVariableContext = new(global, moduleContext);
			moduleContext.GlobalVariables.Add(global, globalVariableContext);
		}

		moduleContext.AssignGlobalVariableNames();

		moduleContext.CreateFunctions();

		moduleContext.AssignFunctionNames();

		moduleContext.InitializeMethodSignatures();

		foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
		{
			globalVariableContext.CreateFields();
		}

		foreach (FunctionContext functionContext in moduleContext.Methods.Values)
		{
			if (IntrinsicFunctionImplementer.TryHandleIntrinsicFunction(functionContext))
			{
				continue;
			}

			CilInstructionCollection instructions = functionContext.Definition.CilMethodBody!.Instructions;

			functionContext.AnalyzeDataFlow();

			// Create local variables for all instructions
			foreach (InstructionContext instruction in functionContext.Instructions)
			{
				instruction.CreateLocal(instructions);
			}

			// Add instructions to the method bodies
			foreach (BasicBlockContext basicBlock in functionContext.BasicBlocks)
			{
				CilInstructionLabel blockLabel = functionContext.Labels[basicBlock.Block];
				blockLabel.Instruction = instructions.Add(CilOpCodes.Nop);

				foreach (InstructionContext instructionContext in basicBlock.Instructions)
				{
					instructionContext.AddInstructions(instructions);
				}
			}

			instructions.OptimizeMacros();

			functionContext.MaybeAddStructReturnMethod();
		}

		// Structs are discovered dynamically, so we need to assign names after all methods are created.
		moduleContext.AssignStructNames();

		foreach (InlineArrayContext inlineArray in moduleContext.InlineArrayTypes.Values)
		{
			inlineArray.ImplementInterface();
		}

		return fixAssemblyReferences ? moduleDefinition.FixCorLibAssemblyReferences() : moduleDefinition;
	}
}
