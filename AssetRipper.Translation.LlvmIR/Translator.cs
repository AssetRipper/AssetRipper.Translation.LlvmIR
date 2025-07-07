using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Instructions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AssetRipper.Translation.LlvmIR;

public static unsafe class Translator
{
	static Translator()
	{
		Patches.Apply();
	}

	public static ModuleDefinition Translate(string name, string content, TranslatorOptions? options = null)
	{
		return Translate(name, Encoding.UTF8.GetBytes(content), options);
	}

	public static ModuleDefinition Translate(string name, ReadOnlySpan<byte> content, TranslatorOptions? options = null)
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
					return Translate(module, options ?? new());
				}
				finally
				{
					// https://github.com/dotnet/LLVMSharp/issues/234
					//context.Dispose();
				}
			}
			finally
			{
				// This fails randomly with no real explanation.
				// I'm fairly certain that the IR text data is only referenced (not copied),
				// so the memory leak of not disposing the buffer is probably not a big deal.
				// https://github.com/dotnet/LLVMSharp/issues/234
				//LLVM.DisposeMemoryBuffer(buffer);

				Marshal.FreeHGlobal(namePtr);
			}
		}
	}

	private static ModuleDefinition Translate(LLVMModuleRef module, TranslatorOptions options)
	{
		{
			var globals = module.GetGlobals().ToList();
			var aliases = module.GetGlobalAliases().ToList();
			var ifuncs = module.GetGlobalIFuncs().ToList();
			var metadataList = module.GetNamedMetadata().ToList();
		}

		ModuleDefinition moduleDefinition = new(string.IsNullOrEmpty(options.ModuleName) ? "ConvertedCpp" : options.ModuleName, KnownCorLibs.SystemRuntime_v9_0_0_0);

		moduleDefinition.AddTargetFrameworkAttributeForDotNet9();

		ModuleContext moduleContext = new(module, moduleDefinition, options);

		foreach (LLVMValueRef global in module.GetGlobals())
		{
			GlobalVariableContext globalVariableContext = new(global, moduleContext);
			moduleContext.GlobalVariables.Add(global, globalVariableContext);
		}

		moduleContext.AssignGlobalVariableNames();

		moduleContext.CreateFunctions();

		moduleContext.AssignFunctionNames();

		moduleContext.IdentifyFunctionsThatMightThrow();

		foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
		{
			globalVariableContext.CreateProperties();
		}

		moduleContext.FillPointerIndexType();

		foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
		{
			globalVariableContext.InitializeData();
		}

		foreach (FunctionContext functionContext in moduleContext.Methods.Values)
		{
			functionContext.AddNameAttributes(functionContext.Definition);

			if (IntrinsicFunctionImplementer.TryHandleIntrinsicFunction(functionContext))
			{
				continue;
			}

			CilInstructionCollection instructions = functionContext.Definition.CilMethodBody!.Instructions;

			functionContext.AnalyzeDataFlow();

			if (functionContext.MightThrowAnException)
			{
				Debug.Assert(functionContext.LocalVariablesType is not null);
				Debug.Assert(functionContext.StackFrameVariable is not null);
				TypeDefinition stackFrameListType = moduleContext.InjectedTypes[typeof(StackFrameList)];

				instructions.Add(CilOpCodes.Ldsflda, stackFrameListType.GetFieldByName(nameof(StackFrameList.Current)));
				instructions.Add(CilOpCodes.Call, stackFrameListType.GetMethodByName(nameof(StackFrameList.New)).MakeGenericInstanceMethod(functionContext.LocalVariablesType.ToTypeSignature()));
				instructions.Add(CilOpCodes.Stloc, functionContext.StackFrameVariable);
			}

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

			functionContext.AddPublicImplementation();
		}

		// Structs are discovered dynamically, so we need to assign names after all methods are created.
		moduleContext.AssignStructNames();

		return options.FixAssemblyReferences ? moduleDefinition.FixCorLibAssemblyReferences() : moduleDefinition;
	}
}
