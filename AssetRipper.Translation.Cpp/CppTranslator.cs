using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.Cpp.Extensions;
using AssetRipper.Translation.Cpp.Instructions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace AssetRipper.Translation.Cpp;

public static unsafe class CppTranslator
{
	static CppTranslator()
	{
		Patches.Apply();
	}

	public static ModuleDefinition Translate(string name, string content)
	{
		return Translate(name, Encoding.UTF8.GetBytes(content));
	}

	public static ModuleDefinition Translate(string name, ReadOnlySpan<byte> content)
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
					return Translate(module);
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

	private static ModuleDefinition Translate(LLVMModuleRef module, bool removeDeadCode = false)
	{
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

		// Target framework attribute
		{
			IMethodDescriptor constructor = moduleDefinition.DefaultImporter.ImportMethod(typeof(TargetFrameworkAttribute).GetConstructors().Single());

			CustomAttributeSignature signature = new();

			signature.FixedArguments.Add(new(moduleDefinition.CorLibTypeFactory.String, moduleDefinition.OriginalTargetRuntime.ToString()));
			signature.NamedArguments.Add(new(
				CustomAttributeArgumentMemberType.Property,
				nameof(TargetFrameworkAttribute.FrameworkDisplayName),
				moduleDefinition.CorLibTypeFactory.String,
				new(moduleDefinition.CorLibTypeFactory.String, ".NET 9.0")));

			CustomAttribute attribute = new((ICustomAttributeType)constructor, signature);

			if (moduleDefinition.Assembly is null)
			{
				AssemblyDefinition assembly = new(moduleDefinition.Name, new Version(1, 0, 0, 0));
				assembly.Modules.Add(moduleDefinition);
				assembly.CustomAttributes.Add(attribute);
			}
			else
			{
				moduleDefinition.Assembly.CustomAttributes.Add(attribute);
			}
		}

		ModuleContext moduleContext = new(module, moduleDefinition);

		foreach (LLVMValueRef global in module.GetGlobals())
		{
			GlobalVariableContext globalVariableContext = new(global, moduleContext);
			moduleContext.GlobalVariables.Add(global, globalVariableContext);
		}

		moduleContext.AssignGlobalVariableNames();

		foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
		{
			globalVariableContext.CreateFields();
		}

		moduleContext.CreateFunctions();

		moduleContext.AssignFunctionNames();

		moduleContext.InitializeMethodSignatures();

		foreach (FunctionContext functionContext in moduleContext.Methods.Values)
		{
			functionContext.CreateLabelsForBasicBlocks();

			functionContext.Analyze();
		}

		foreach (FunctionContext functionContext in moduleContext.Methods.Values)
		{
			functionContext.Definition.Parameters.PullUpdatesFromMethodSignature();
		}

		foreach ((LLVMValueRef function, FunctionContext functionContext) in moduleContext.Methods)
		{
			MethodDefinition method = functionContext.Definition;
			Debug.Assert(method.CilMethodBody is not null);

			CilInstructionCollection instructions = method.CilMethodBody.Instructions;

			foreach (InstructionContext instruction in functionContext.Instructions)
			{
				switch (instruction)
				{
					case AllocaInstructionContext allocaInstruction:
						{
							if (allocaInstruction.FixedSize != 1)
							{
								throw new NotSupportedException("Fixed size array not supported");
							}
							else
							{
								allocaInstruction.DataLocal = instructions.AddLocalVariable(allocaInstruction.AllocatedTypeSignature);
							}

							if (allocaInstruction.Accessors.Count > 0)
							{
								CilLocalVariable pointerLocal = instructions.AddLocalVariable(allocaInstruction.PointerTypeSignature);
								allocaInstruction.PointerLocal = pointerLocal;
								instruction.Function.InstructionLocals[instruction.Instruction] = pointerLocal;
							}
						}
						break;
					default:
						if (instruction.HasResult)
						{
							CilLocalVariable resultLocal = instructions.AddLocalVariable(instruction.ResultTypeSignature);
							instruction.Function.InstructionLocals[instruction.Instruction] = resultLocal;
						}
						break;
				}
			}
		}

		foreach ((LLVMValueRef function, FunctionContext functionContext) in moduleContext.Methods)
		{
			MethodDefinition method = functionContext.Definition;

			CilInstructionCollection instructions = method.CilMethodBody!.Instructions;

			if (functionContext.BasicBlocks.Count == 0)
			{
				if (!IntrinsicFunctionImplementer.TryFillIntrinsicFunction(functionContext))
				{
					instructions.Add(CilOpCodes.Ldnull);
					instructions.Add(CilOpCodes.Throw);
				}
				continue;
			}

			foreach (BasicBlockContext basicBlock in functionContext.BasicBlocks)
			{
				CilInstructionLabel blockLabel = functionContext.Labels[basicBlock.Block];
				blockLabel.Instruction = instructions.Add(CilOpCodes.Nop);

				foreach (InstructionContext instructionContext in basicBlock.Instructions)
				{
					LLVMValueRef instruction = instructionContext.Instruction;
					switch (instructionContext)
					{
						case AllocaInstructionContext allocaInstructionContext:
							{
								TypeSignature allocatedTypeSignature = allocaInstructionContext.AllocatedTypeSignature;

								if (allocaInstructionContext.DataLocal is null)
								{
									Debug.Assert(allocaInstructionContext.PointerLocal is not null);
									throw new NotSupportedException("Stack allocated data not currently supported");
								}
								else if (allocaInstructionContext.PointerLocal is not null)
								{
									//Zero out the memory
									instructions.Add(CilOpCodes.Ldloca, allocaInstructionContext.DataLocal);
									instructions.Add(CilOpCodes.Initobj, allocatedTypeSignature.ToTypeDefOrRef());

									//Might need slight modifications for fixed size arrays.
									instructions.Add(CilOpCodes.Ldloca, allocaInstructionContext.DataLocal);
									instructions.Add(CilOpCodes.Stloc, allocaInstructionContext.PointerLocal);
								}
							}
							break;
						case LoadInstructionContext loadInstructionContext:
							{
								if (loadInstructionContext.SourceInstruction is AllocaInstructionContext { DataLocal: not null } allocaSource
									&& IsCompatible(allocaSource, loadInstructionContext))
								{
									if (allocaSource.DataLocal.VariableType is PointerTypeSignature
										|| SignatureComparer.Default.Equals(allocaSource.DataLocal.VariableType, loadInstructionContext.ResultTypeSignature))
									{
										instructions.Add(CilOpCodes.Ldloc, allocaSource.DataLocal);
									}
									else
									{
										instructions.Add(CilOpCodes.Ldloca, allocaSource.DataLocal);
										instructions.AddLoadIndirect(loadInstructionContext.ResultTypeSignature);
									}
								}
								else
								{
									functionContext.LoadOperand(loadInstructionContext.SourceOperand);
									instructions.AddLoadIndirect(loadInstructionContext.ResultTypeSignature);
								}

								instructions.Add(CilOpCodes.Stloc, functionContext.InstructionLocals[instructionContext.Instruction]);

								static bool IsCompatible(AllocaInstructionContext allocaSource, LoadInstructionContext loadInstructionContext)
								{
									Debug.Assert(allocaSource.DataLocal is not null);

									if (allocaSource.DataLocal.VariableType is PointerTypeSignature && loadInstructionContext.ResultTypeSignature is PointerTypeSignature)
									{
										return true;
									}

									LLVMModuleRef module = allocaSource.Function.Module.Module;
									if (allocaSource.AllocatedType.GetABISize(module) == loadInstructionContext.Instruction.TypeOf.GetABISize(module))
									{
										return true;
									}

									return false;
								}
							}
							break;
						case StoreInstructionContext storeInstructionContext:
							{
								if (storeInstructionContext.DestinationInstruction is AllocaInstructionContext { DataLocal: not null } allocaDestination
									&& IsCompatible(allocaDestination, storeInstructionContext))
								{
									if (allocaDestination.DataLocal.VariableType is PointerTypeSignature
										|| SignatureComparer.Default.Equals(allocaDestination.DataLocal.VariableType, storeInstructionContext.StoreTypeSignature))
									{
										functionContext.LoadOperand(storeInstructionContext.SourceOperand);
										instructions.Add(CilOpCodes.Stloc, allocaDestination.DataLocal);
									}
									else
									{
										instructions.Add(CilOpCodes.Ldloca, allocaDestination.DataLocal);
										functionContext.LoadOperand(storeInstructionContext.SourceOperand);
										instructions.AddStoreIndirect(storeInstructionContext.StoreTypeSignature);
									}
								}
								else
								{
									functionContext.LoadOperand(storeInstructionContext.DestinationOperand);
									functionContext.LoadOperand(storeInstructionContext.SourceOperand);
									instructions.AddStoreIndirect(storeInstructionContext.StoreTypeSignature);
								}

								static bool IsCompatible(AllocaInstructionContext allocaDestination, StoreInstructionContext storeInstructionContext)
								{
									Debug.Assert(allocaDestination.DataLocal is not null);

									if (allocaDestination.DataLocal.VariableType is PointerTypeSignature && storeInstructionContext.StoreTypeSignature is PointerTypeSignature)
									{
										return true;
									}

									LLVMModuleRef module = allocaDestination.Function.Module.Module;
									if (allocaDestination.AllocatedType.GetABISize(module) == storeInstructionContext.StoreType.GetABISize(module))
									{
										return true;
									}

									return false;
								}
							}
							break;
						case CallInstructionContext callInstructionContext:
							{
								for (int i = 0; i < instructionContext.Operands.Length - 1; i++)
								{
									instructionContext.LoadOperand(i);
								}

								if (callInstructionContext.FunctionCalled is null)
								{
									instructionContext.LoadOperand(instructionContext.Operands.Length - 1);
									instructions.Add(CilOpCodes.Calli, callInstructionContext.MakeStandaloneSignature());
								}
								else
								{
									instructions.Add(CilOpCodes.Call, callInstructionContext.FunctionCalled.Definition);
								}

								if (callInstructionContext.HasResult)
								{
									instructions.Add(CilOpCodes.Stloc, functionContext.InstructionLocals[instruction]);
								}
							}
							break;
						case UnaryMathInstructionContext unaryMathInstructionContext:
							{
								functionContext.LoadOperand(unaryMathInstructionContext.Operand);
								instructions.Add(unaryMathInstructionContext.CilOpCode);
								instructions.Add(CilOpCodes.Stloc, functionContext.InstructionLocals[instructionContext.Instruction]);
							}
							break;
						case BinaryMathInstructionContext binaryMathInstructionContext:
							{
								functionContext.LoadOperand(binaryMathInstructionContext.Operand1);
								functionContext.LoadOperand(binaryMathInstructionContext.Operand2);
								instructions.Add(binaryMathInstructionContext.CilOpCode);
								instructions.Add(CilOpCodes.Stloc, functionContext.InstructionLocals[instructionContext.Instruction]);
							}
							break;
						case NumericComparisonInstructionContext numericComparisonInstructionContext:
							{
								functionContext.LoadOperand(numericComparisonInstructionContext.Operand1);
								functionContext.LoadOperand(numericComparisonInstructionContext.Operand2);
								numericComparisonInstructionContext.AddComparisonInstruction(instructions);
								instructions.Add(CilOpCodes.Stloc, functionContext.InstructionLocals[instructionContext.Instruction]);
							}
							break;
						case NumericConversionInstructionContext integerExtendInstructionContext:
							{
								functionContext.LoadOperand(integerExtendInstructionContext.Operand);
								instructions.Add(integerExtendInstructionContext.CilOpCode);
								instructions.Add(CilOpCodes.Stloc, functionContext.InstructionLocals[instructionContext.Instruction]);
							}
							break;
						case BranchInstructionContext branchInstructionContext:
							branchInstructionContext.AddBranchInstruction();
							break;
						case ReturnInstructionContext returnInstructionContext:
							{
								if (returnInstructionContext.HasReturnValue)
								{
									functionContext.LoadOperand(returnInstructionContext.ResultOperand);
								}
								instructions.Add(CilOpCodes.Ret);
							}
							break;
						case PhiInstructionContext:
							// Handled by branches
							break;
						case GetElementPointerInstructionContext gepInstructionContext:
							{
								//This is the pointer. It's generally void* due to stripping.
								functionContext.LoadOperand(gepInstructionContext.SourceOperand);//Pointer

								//This isn't strictly necessary, but it might make ILSpy output better someday.
								CilLocalVariable pointerLocal = instructions.AddLocalVariable(gepInstructionContext.SourceElementTypeSignature.MakePointerType());
								functionContext.CilInstructions.Add(CilOpCodes.Stloc, pointerLocal);
								functionContext.CilInstructions.Add(CilOpCodes.Ldloc, pointerLocal);

								//This is the index, which might be a constant.
								functionContext.LoadOperand(instructionContext.Operands[1], out _);
								CilInstruction previousInstruction = functionContext.CilInstructions[^1];

								if (previousInstruction.IsLoadConstantInteger(out long value) && value == 0)
								{
									//Remove the Ldc_I4_0
									//There's no need to add it to the pointer.
									functionContext.CilInstructions.Pop();
								}
								else if (gepInstructionContext.SourceElementTypeSignature.TryGetSize(out int size))
								{
									if (size == 1)
									{
										functionContext.CilInstructions.Add(CilOpCodes.Conv_I);
									}
									else
									{
										if (previousInstruction.IsLoadConstantInteger(out value))
										{
											previousInstruction.OpCode = CilOpCodes.Ldc_I4;
											previousInstruction.Operand = (int)(value * size);
											functionContext.CilInstructions.Add(CilOpCodes.Conv_I);
										}
										else
										{
											functionContext.CilInstructions.Add(CilOpCodes.Conv_I);
											functionContext.CilInstructions.Add(CilOpCodes.Ldc_I4, size);
											functionContext.CilInstructions.Add(CilOpCodes.Mul);
										}
									}
									functionContext.CilInstructions.Add(CilOpCodes.Add);
								}
								else
								{
									functionContext.CilInstructions.Add(CilOpCodes.Conv_I);
									functionContext.CilInstructions.Add(CilOpCodes.Sizeof, gepInstructionContext.SourceElementTypeSignature.ToTypeDefOrRef());
									functionContext.CilInstructions.Add(CilOpCodes.Mul);
									functionContext.CilInstructions.Add(CilOpCodes.Add);
								}

								TypeSignature currentType = gepInstructionContext.SourceElementTypeSignature;
								for (int i = 2; i < instructionContext.Operands.Length; i++)
								{
									LLVMValueRef operand = instructionContext.Operands[i];
									if (currentType is TypeDefOrRefSignature structTypeSignature)
									{
										TypeDefinition structType = (TypeDefinition)structTypeSignature.ToTypeDefOrRef();
										if (operand.Kind == LLVMValueKind.LLVMConstantIntValueKind)
										{
											long index = operand.ConstIntSExt;
											string fieldName = $"field_{index}";
											FieldDefinition field = structType.Fields.First(t => t.Name == fieldName);
											functionContext.CilInstructions.Add(CilOpCodes.Ldflda, field);
											currentType = field.Signature!.FieldType;
										}
										else
										{
											throw new NotSupportedException();
										}
									}
									else if (currentType is CorLibTypeSignature)
									{
										throw new NotSupportedException();
									}
									else
									{
										throw new NotSupportedException();
									}
								}

								functionContext.CilInstructions.Add(CilOpCodes.Stloc, functionContext.InstructionLocals[gepInstructionContext.Instruction]);
							}
							break;
						default:
							switch (instruction.InstructionOpcode)
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
								case LLVMOpcode.LLVMFPToUI:
									goto default;
								case LLVMOpcode.LLVMFPToSI:
									goto default;
								case LLVMOpcode.LLVMUIToFP:
									goto default;
								case LLVMOpcode.LLVMSIToFP:
									goto default;
								case LLVMOpcode.LLVMIntToPtr:
									goto default;
								case LLVMOpcode.LLVMBitCast:
									goto default;
								case LLVMOpcode.LLVMAddrSpaceCast:
									goto default;
								case LLVMOpcode.LLVMSelect:
									goto default;
								case LLVMOpcode.LLVMUserOp1:
									goto default;
								case LLVMOpcode.LLVMUserOp2:
									goto default;
								case LLVMOpcode.LLVMVAArg:
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
							break;
					}
				}
			}

			instructions.OptimizeMacros();
		}

		// Add struct return type methods
		foreach (FunctionContext functionContext in moduleContext.Methods.Values)
		{
			if (!functionContext.TryGetStructReturnType(out LLVMTypeRef structReturnType))
			{
				continue;
			}

			TypeSignature returnTypeSignature = functionContext.Module.GetTypeSignature(structReturnType);

			MethodDefinition method = functionContext.Definition;

			MethodDefinition newMethod = new(method.Name, method.Attributes, MethodSignature.CreateStatic(returnTypeSignature, method.Parameters.Skip(1).Select(p => p.ParameterType)));
			method.DeclaringType!.Methods.Add(newMethod);
			newMethod.CilMethodBody = new(newMethod);

			CilInstructionCollection instructions = newMethod.CilMethodBody.Instructions;
			CilLocalVariable returnLocal = instructions.AddLocalVariable(returnTypeSignature);
			instructions.Add(CilOpCodes.Ldloca, returnLocal);
			instructions.Add(CilOpCodes.Initobj, returnTypeSignature.ToTypeDefOrRef());
			instructions.Add(CilOpCodes.Ldloca, returnLocal);
			foreach (Parameter parameter in newMethod.Parameters)
			{
				instructions.Add(CilOpCodes.Ldarg, parameter);
			}
			instructions.Add(CilOpCodes.Call, method);
			instructions.Add(CilOpCodes.Ldloc, returnLocal);
			instructions.Add(CilOpCodes.Ret);
			instructions.OptimizeMacros();

			// Hide the original method
			method.IsAssembly = true;

			// Annotate the original return parameter
			method.Parameters[0].GetOrCreateDefinition().Name = "result";
		}

		moduleContext.AssignStructNames();

		// Fixup references
		{
			MemoryStream stream = new();
			moduleDefinition.Write(stream);
			stream.Position = 0;
			ModuleDefinition moduleDefinition2 = ModuleDefinition.FromBytes(stream.ToArray());
			AssemblyReference? systemPrivateCorLib = moduleDefinition2.AssemblyReferences.FirstOrDefault(a => a.Name == "System.Private.CoreLib");
			AssemblyReference? systemRuntime = moduleDefinition2.AssemblyReferences.FirstOrDefault(a => a.Name == "System.Runtime");
			if (systemPrivateCorLib is not null && systemRuntime is not null)
			{
				foreach (TypeReference typeReference in moduleDefinition2.GetImportedTypeReferences())
				{
					if (typeReference.Scope == systemPrivateCorLib)
					{
						typeReference.Scope = systemRuntime;
					}
				}
				return moduleDefinition2;
			}
		}

		return moduleDefinition;
	}
}
