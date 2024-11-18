using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.Cpp;

public static unsafe class CppTranslator
{
	static CppTranslator()
	{
		Patches.Apply();
	}

	public static void Translate(string inputPath, string outputPath, bool removeDeadCode = false)
	{
		ModuleDefinition moduleDefinition = new("ConvertedCpp", KnownCorLibs.SystemRuntime_v8_0_0_0);
		LLVMMemoryBufferRef buffer = LoadIR(inputPath);
		try
		{
			LLVMContextRef context = LLVMContextRef.Create();
			try
			{
				LLVMModuleRef module = context.ParseIR(buffer);

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

				ModuleContext moduleContext = new(module, moduleDefinition);

				foreach (LLVMValueRef global in module.GetGlobals())
				{
					Debug.Assert(global.OperandCount == 1);
					LLVMValueRef operand = global.GetOperand(0);
					//LLVMTypeRef type = operand.TypeOf;
					//LLVMTypeRef globalType = LLVM.GlobalGetValueType(global);
					//https://github.com/llvm/llvm-project/blob/ccf357ff643c6af86bb459eba5a00f40f1dcaf22/llvm/include/llvm/IR/Constants.h#L584

					ReadOnlySpan<byte> data = LibLLVMSharp.ConstantDataArrayGetData(operand);

					FieldDefinition field = moduleContext.AddStoredDataField(data.ToArray());

					moduleContext.GlobalConstants.Add(global, field);
				}

				moduleContext.CreateFunctions();

				moduleContext.AssignFunctionNames();

				moduleContext.InitializeMethodSignatures();

				foreach ((LLVMValueRef function, FunctionContext functionContext) in moduleContext.Methods)
				{
					MethodDefinition method = functionContext.Definition;
					Debug.Assert(method.CilMethodBody is not null);

					CilInstructionCollection instructions = method.CilMethodBody.Instructions;

					functionContext.CreateLabelsForBasicBlocks();

					functionContext.Analyze();

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

					functionContext.FixMethodReturnType();
				}

				foreach ((LLVMValueRef function, FunctionContext functionContext) in moduleContext.Methods)
				{
					MethodDefinition method = functionContext.Definition;

					CilInstructionCollection instructions = method.CilMethodBody!.Instructions;

					if (functionContext.BasicBlocks.Count == 0)
					{
						instructions.Add(CilOpCodes.Ldnull);
						instructions.Add(CilOpCodes.Throw);
						continue;
					}

					functionContext.CreateLabelsForBasicBlocks();

					functionContext.Analyze();

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
											throw new NotSupportedException("Stack allocated data not currently supported");
										}
										else
										{
											// We could zero out the memory here, but it's not necessary because alloca doesn't guarantee zero initialization.
											//Zero out the memory
											//instructions.Add(CilOpCodes.Ldloca, allocaInstructionContext.DataLocal);
											//instructions.Add(CilOpCodes.Initobj, allocatedTypeSignature.ToTypeDefOrRef());

											if (allocaInstructionContext.PointerLocal is not null)
											{
												//Might need slight modifications for fixed size arrays.
												instructions.Add(CilOpCodes.Ldloca, allocaInstructionContext.DataLocal);
												instructions.Add(CilOpCodes.Stloc, allocaInstructionContext.PointerLocal);
											}
										}
									}
									break;
								case LoadInstructionContext loadInstructionContext:
									{
										TypeSignature loadTypeSignature = loadInstructionContext.ResultTypeSignature ?? throw new NullReferenceException();

										if (loadInstructionContext.SourceInstruction is AllocaInstructionContext { DataLocal: not null } allocaSource
											//&& SignatureComparer.Default.Equals(allocaSource.DataLocal.VariableType, loadTypeSignature)// Disabled because of incorrect types
											)
										{
											if (SignatureComparer.Default.Equals(allocaSource.DataLocal.VariableType, loadTypeSignature))
											{
												instructions.Add(CilOpCodes.Ldloc, allocaSource.DataLocal);
											}
											else
											{
												instructions.Add(CilOpCodes.Ldloca, allocaSource.DataLocal);
												instructions.AddLoadIndirect(loadTypeSignature);
											}
										}
										else
										{
											CilLocalVariable addressLocal = functionContext.InstructionLocals[loadInstructionContext.SourceOperand];

											instructions.Add(CilOpCodes.Ldloc, addressLocal);
											instructions.AddLoadIndirect(loadTypeSignature);
										}

										instructions.Add(CilOpCodes.Stloc, functionContext.InstructionLocals[instructionContext.Instruction]);
									}
									break;
								case StoreInstructionContext storeInstructionContext:
									{
										//https://llvm.org/docs/OpaquePointers.html#migration-instructions
										LLVMTypeRef storeType = instructionContext.Operands[0].TypeOf;
										TypeSignature storeTypeSignature = moduleContext.GetTypeSignature(storeType);

										if (storeInstructionContext.DestinationInstruction is AllocaInstructionContext { DataLocal: not null } allocaDestination
											//&& SignatureComparer.Default.Equals(allocaDestination.DataLocal.VariableType, storeTypeSignature)// Disabled because of incorrect parameter types
											)
										{
											if (SignatureComparer.Default.Equals(allocaDestination.DataLocal.VariableType, storeTypeSignature))
											{
												functionContext.LoadOperand(storeInstructionContext.SourceOperand);
												instructions.Add(CilOpCodes.Stloc, allocaDestination.DataLocal);
											}
											else
											{
												instructions.Add(CilOpCodes.Ldloca, allocaDestination.DataLocal);
												functionContext.LoadOperand(storeInstructionContext.SourceOperand);
												instructions.AddStoreIndirect(storeTypeSignature);
											}
										}
										else
										{
											CilLocalVariable addressLocal = functionContext.InstructionLocals[instructionContext.Operands[1]];

											instructions.Add(CilOpCodes.Ldloc, addressLocal);
											functionContext.LoadOperand(storeInstructionContext.SourceOperand);
											instructions.AddStoreIndirect(storeTypeSignature);
										}
									}
									break;
								case CallInstructionContext callInstructionContext:
									{
										for (int i = 0; i < instructionContext.Operands.Length - 1; i++)
										{
											instructionContext.LoadOperand(i);
										}
										instructions.Add(CilOpCodes.Call, callInstructionContext.FunctionCalled.Definition);
										if (callInstructionContext.HasResult)
										{
											// Todo: pop if the return value isn't used
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
										functionContext.LoadOperand(instructionContext.Operands[0]);//Pointer

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
										case LLVMOpcode.LLVMPtrToInt:
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
			}
			finally
			{
				context.Dispose();
			}
		}
		finally
		{
			//LLVM.DisposeMemoryBuffer(buffer);
		}

		moduleDefinition.Write(outputPath);
	}

	private static LLVMMemoryBufferRef LoadIR(string path)
	{
		byte[] data = File.ReadAllBytes(path);
		LLVMMemoryBufferRef buffer;
		fixed (byte* ptr = data)
		{
			nint name = Marshal.StringToHGlobalAnsi(Path.GetFileName(path));
			try
			{
				buffer = LLVM.CreateMemoryBufferWithMemoryRangeCopy((sbyte*)ptr, (nuint)data.Length, (sbyte*)name);
			}
			finally
			{
				Marshal.FreeHGlobal(name);
			}
		}

		return buffer;
	}
}
