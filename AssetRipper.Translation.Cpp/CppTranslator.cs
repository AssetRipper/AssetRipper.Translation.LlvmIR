using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.ExceptionHandling;
using AssetRipper.Translation.Cpp.Extensions;
using AssetRipper.Translation.Cpp.Instructions;
using LLVMSharp.Interop;
using System.Diagnostics.CodeAnalysis;
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
		RemoveBasicBlocksWithSingleBranch(module);

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

			ISeseRegion liftedRegion = BlockLifter.LiftBasicBlocks(functionContext.BasicBlocks[0]);

			// Add instructions to the method bodies
			AddInstructions(instructions, functionContext, liftedRegion);

			instructions.OptimizeMacros();

			functionContext.MaybeAddStructReturnMethod();
		}

		// Structs are discovered dynamically, so we need to assign names after all methods are created.
		moduleContext.AssignStructNames();

		return fixAssemblyReferences ? moduleDefinition.FixCorLibAssemblyReferences() : moduleDefinition;
	}

	private static void RemoveBasicBlocksWithSingleBranch(LLVMModuleRef module)
	{
		foreach (LLVMValueRef function in module.GetFunctions())
		{
			bool changed;
			do
			{
				changed = false;
				foreach (LLVMBasicBlockRef block in function.GetBasicBlocks())
				{
					if (!block.TryGetSingleInstruction(out LLVMValueRef instruction) || instruction.InstructionOpcode != LLVMOpcode.LLVMBr || instruction.IsConditional)
					{
						continue;
					}
					LLVMValueRef[] operands = instruction.GetOperands();
					if (operands.Length != 1)
					{
						continue;
					}
					LLVMValueRef operand = operands[0];
					if (operand.Kind != LLVMValueKind.LLVMBasicBlockValueKind)
					{
						continue;
					}
					LLVMBasicBlockRef targetBlock = operand.AsBasicBlock();
					if (targetBlock == block)
					{
						// Just to be safe
						continue;
					}

					block.AsValue().ReplaceAllUsesWith(operand);
					block.RemoveFromParent();
					//LLVM.DeleteBasicBlock(block);
					changed = true;
					break;
				}
			} while (changed);
		}
	}

	private static void AddInstructions(CilInstructionCollection instructions, FunctionContext functionContext, ISeseRegion liftedRegion)
	{
		switch (liftedRegion)
		{
			case ISeseRegionAlias alias:
				AddInstructions(instructions, functionContext, alias.Original);
				break;
			case ICompositeSeseRegion composite:
				if (TryMatchExceptionHandler(composite, out ISeseRegion? protectedRegion, out ISeseRegion? exceptionHandlerSwitch, out IReadOnlyList<ISeseRegion>? exceptionHandlers))
				{
					CatchSwitchInstructionContext catchSwitch = GetInstructions<CatchSwitchInstructionContext>(exceptionHandlerSwitch).First();

					// try
					{
						catchSwitch.TryStartLabel.Instruction = instructions.Add(CilOpCodes.Nop);
						AddInstructions(instructions, functionContext, protectedRegion);

						if (protectedRegion.NormalSuccessors.Count is 1)
						{
							ISeseRegion defaultRegion = protectedRegion.NormalSuccessors[0];
							BasicBlockContext defaultBlock = GetBasicBlocks(defaultRegion).First();
							CilInstructionLabel defaultLabel = functionContext.Labels[defaultBlock.Block];
							catchSwitch.TryEndLabel.Instruction = instructions.Add(CilOpCodes.Leave, defaultLabel);
						}
						else
						{
							CilInstructionLabel defaultLabel = new();
							catchSwitch.TryEndLabel.Instruction = instructions.Add(CilOpCodes.Leave, defaultLabel);
							defaultLabel.Instruction = instructions.Add(CilOpCodes.Ldnull);
							instructions.Add(CilOpCodes.Throw);
						}
					}

					AddInstructions(instructions, functionContext, exceptionHandlerSwitch);

					// catch
					foreach (ISeseRegion exceptionHandler in exceptionHandlers)
					{
						AddInstructions(instructions, functionContext, exceptionHandler);
					}
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

		static bool TryMatchExceptionHandler(ICompositeSeseRegion composite, [NotNullWhen(true)] out ISeseRegion? protectedRegion, [NotNullWhen(true)] out ISeseRegion? exceptionHandlerSwitch, [NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? exceptionHandlers)
		{
			if (composite.Children.Count < 3)
			{
				return False(out protectedRegion, out exceptionHandlerSwitch, out exceptionHandlers);
			}

			exceptionHandlerSwitch = composite.Children.FirstOrDefault(x => x.IsExceptionHandlerSwitch);
			if (exceptionHandlerSwitch is null or { AllPredecessors.Count: not 1 })
			{
				return False(out protectedRegion, out exceptionHandlerSwitch, out exceptionHandlers);
			}

			protectedRegion = exceptionHandlerSwitch.AllPredecessors[0];
			if (protectedRegion.NormalSuccessors.Count is not 0 and not 1 || !composite.Children.Contains(protectedRegion))
			{
				return False(out protectedRegion, out exceptionHandlerSwitch, out exceptionHandlers);
			}

			exceptionHandlers = composite.Children.Except([protectedRegion, exceptionHandlerSwitch]).ToList();
			foreach (ISeseRegion exceptionHandler in exceptionHandlers)
			{
				if (!exceptionHandler.IsSelfContainedExceptionHandler || exceptionHandler.AllPredecessors.Count != 1 || exceptionHandler.AllPredecessors[0] != exceptionHandlerSwitch)
				{
					return False(out protectedRegion, out exceptionHandlerSwitch, out exceptionHandlers);
				}
			}

			return true;

			static bool False([NotNullWhen(true)] out ISeseRegion? protectedRegion, [NotNullWhen(true)] out ISeseRegion? exceptionHandlerSwitch, [NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? exceptionHandlers)
			{
				protectedRegion = null;
				exceptionHandlerSwitch = null;
				exceptionHandlers = null;
				return false;
			}
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
