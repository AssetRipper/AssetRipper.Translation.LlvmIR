using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.ExceptionHandling;
using AssetRipper.Translation.Cpp.Extensions;
using AssetRipper.Translation.Cpp.Instructions;
using LLVMSharp.Interop;
using System.Diagnostics;
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
					// Todo: write a libLLVMSharp wrapper with a try-catch around Dispose
					//context.Dispose();
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
			globalVariableContext.CreateProperties();
		}

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

			// Create local variables for all instructions
			foreach (InstructionContext instruction in functionContext.Instructions)
			{
				instruction.CreateLocal(instructions);
			}

			IReadOnlyList<ISeseRegion> liftedRegions = BlockLifter.LiftBasicBlocks(functionContext.BasicBlocks[0]);
			if (liftedRegions.Count != 1)
			{
				Debugger.Break();
			}
			ISeseRegion liftedRegion = liftedRegions.AsSingleRegion();

			// Add instructions to the method bodies
			AddInstructions(instructions, functionContext, liftedRegion);

			instructions.OptimizeMacros();

			functionContext.MaybeAddStructReturnMethod();
		}

		// Structs are discovered dynamically, so we need to assign names after all methods are created.
		moduleContext.AssignStructNames();

		return fixAssemblyReferences ? moduleDefinition.FixCorLibAssemblyReferences() : moduleDefinition;
	}

	private static void AddInstructions(CilInstructionCollection instructions, FunctionContext functionContext, ISeseRegion liftedRegion)
	{
		switch (liftedRegion)
		{
			case ISeseRegionAlias alias:
				AddInstructions(instructions, functionContext, alias.Original);
				break;
			case ICompositeSeseRegion composite:
				if (composite is ProtectedRegionWithExceptionHandlers composite2)
				{
					HashSet<BasicBlockContext> containedBlocks = GetBasicBlocks(composite2.ProtectedRegion).ToHashSet();
					foreach (InstructionContext instruction in GetInstructions(composite2.ProtectedRegion))
					{
						if (instruction is UnconditionalBranchInstructionContext unconditionalBranch)
						{
							Debug.Assert(unconditionalBranch.TargetBlock is not null);
							if (!containedBlocks.Contains(unconditionalBranch.TargetBlock))
							{
								unconditionalBranch.IsLeave = true;
							}
						}
						else if (instruction is InvokeInstructionContext invoke)
						{
							Debug.Assert(invoke.DefaultBlock is not null);
							if (!containedBlocks.Contains(invoke.DefaultBlock))
							{
								invoke.IsLeave = true;
							}
						}
					}

					int tryStartIndex = instructions.Count;
					AddInstructions(instructions, functionContext, composite2.ProtectedRegion);
					CilInstructionLabel tryStartLabel = new(instructions[tryStartIndex]);
					CilInstructionLabel tryEndLabel = new(instructions[^1]);

					int handlerStartIndex = instructions.Count;
					foreach (ISeseRegion exceptionHandler in composite2.ExceptionHandlingRegions)
					{
						AddInstructions(instructions, functionContext, exceptionHandler);
					}
					CilInstructionLabel handlerStartLabel = new(instructions[handlerStartIndex]);
					CilInstructionLabel handlerEndLabel = new(instructions[^1]);

					instructions.Owner.ExceptionHandlers.Add(new CilExceptionHandler
					{
						TryStart = tryStartLabel,
						TryEnd = tryEndLabel,
						HandlerStart = handlerStartLabel,
						HandlerEnd = handlerEndLabel,
						HandlerType = CilExceptionHandlerType.Exception,
						ExceptionType = functionContext.Module.Definition.CorLibTypeFactory.Object.ToTypeDefOrRef(),
					});
				}
				else
				{
					foreach (ISeseRegion region in composite.Children)
					{
						AddInstructions(instructions, functionContext, region);
					}
				}

				break;
			case BasicBlockContext basicBlock:
				{
					CilInstructionLabel blockLabel = functionContext.Labels[basicBlock.Block];
					blockLabel.Instruction = instructions.Add(CilOpCodes.Nop);

					foreach (InstructionContext instructionContext in basicBlock.Instructions)
					{
						instructionContext.AddInstructions(instructions);
					}
				}
				break;
			default:
				throw new NotSupportedException($"Unsupported lifted region type: {liftedRegion.GetType()}");
		}
	}

	private static IEnumerable<BasicBlockContext> GetBasicBlocks(ISeseRegion liftedRegion)
	{
		Stack<ISeseRegion> stack = new();
		stack.Push(liftedRegion);
		while (stack.Count > 0)
		{
			liftedRegion = stack.Pop();
			switch (liftedRegion)
			{
				case ISeseRegionAlias alias:
					stack.Push(alias.Original);
					break;
				case ICompositeSeseRegion composite:
					for (int i = composite.Children.Count - 1; i >= 0; i--)
					{
						stack.Push(composite.Children[i]);
					}
					break;
				case BasicBlockContext basicBlock:
					yield return basicBlock;
					break;
				default:
					throw new NotSupportedException($"Unsupported lifted region type: {liftedRegion.GetType()}");
			}
		}
	}

	private static IEnumerable<InstructionContext> GetInstructions(ISeseRegion region)
	{
		return GetBasicBlocks(region).SelectMany(x => x.Instructions);
	}

	private static IEnumerable<T> GetInstructions<T>(ISeseRegion region) where T : InstructionContext
	{
		return GetInstructions(region).OfType<T>();
	}
}
