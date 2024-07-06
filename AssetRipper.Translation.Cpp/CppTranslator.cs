using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
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
		ModuleDefinition moduleDefinition = new("ConvertedCpp");
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
				TypeDefinition typeDefinition = new(null, "GlobalMembers", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
				moduleDefinition.TopLevelTypes.Add(typeDefinition);

				ModuleContext moduleContext = new(module, moduleDefinition);

				foreach (LLVMValueRef global in module.GetGlobals())
				{
					Debug.Assert(global.OperandCount == 1);
					LLVMValueRef operand = global.GetOperand(0);
					LLVMTypeRef type = operand.TypeOf;
					LLVMTypeRef globalType = LLVM.GlobalGetValueType(global);
					//I don't think this is feasible right now.
					//I think it needs improvements to libLLVMSharp.
					//https://github.com/llvm/llvm-project/blob/ccf357ff643c6af86bb459eba5a00f40f1dcaf22/llvm/include/llvm/IR/Constants.h#L584
				}

				Dictionary<string, List<MethodDefinition>> demangledNames = new();
				foreach (LLVMValueRef function in module.GetFunctions())
				{
					//string? functionName = LibLLVMSharp.ValueGetDemangledName(function);
					LLVMTypeRef functionType = LibLLVMSharp.FunctionGetFunctionType(function);
					LLVMTypeRef returnType = LibLLVMSharp.FunctionGetReturnType(function);
					TypeSignature returnTypeSignature = moduleContext.GetTypeSignature(returnType);
					MethodSignature signature = MethodSignature.CreateStatic(returnTypeSignature);

					string mangledName = function.Name;
					string demangledName = Demangle(mangledName);
					MethodDefinition method = new(mangledName, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, signature);
					moduleContext.Methods.Add(function, new FunctionContext(function, method, moduleContext));
					if (!demangledNames.TryGetValue(demangledName, out List<MethodDefinition>? list))
					{
						list = new();
						demangledNames.Add(demangledName, list);
					}
					list.Add(method);
				}

				foreach ((string demangledName, List<MethodDefinition> list) in demangledNames)
				{
					if (list.Count == 1)
					{
						list[0].Name = demangledName;
					}
					else
					{
						foreach (MethodDefinition method in list)
						{
							method.Name = NameGenerator.GenerateName(demangledName, method.Name!);
						}
					}
				}

				foreach ((LLVMValueRef function, FunctionContext functionContext) in moduleContext.Methods)
				{
					MethodDefinition method = functionContext.Definition;
					string functionString = function.ToString();

					typeDefinition.Methods.Add(method);

					foreach (LLVMValueRef parameter in function.GetParams())
					{
						LLVMTypeRef type = parameter.TypeOf;
						TypeSignature parameterType = moduleContext.GetTypeSignature(type);
						functionContext.Parameters[parameter] = method.AddParameter(parameterType);
					}

					method.CilMethodBody = new(method);

					CilInstructionCollection instructions = method.CilMethodBody.Instructions;

					if (function.BasicBlocksCount == 0)
					{
						instructions.Add(CilOpCodes.Ldnull);
						instructions.Add(CilOpCodes.Throw);
						continue;
					}

					foreach (LLVMBasicBlockRef basicBlock in function.GetBasicBlocks())
					{
						functionContext.Labels[basicBlock] = new();
					}

					bool hasReturn = false;
					bool hasSingleReturn = true;
					TypeSignature? actualReturnType = null;

					foreach ((LLVMBasicBlockRef basicBlock, CilInstructionLabel blockLabel) in functionContext.Labels)
					{
						blockLabel.Instruction = instructions.Add(CilOpCodes.Nop);

						foreach (LLVMValueRef instruction in basicBlock.GetInstructions())
						{
							InstructionContext instructionContext = new(instruction, functionContext);
							string instructionString = instruction.ToString();
							switch (instruction.InstructionOpcode)
							{
								case LLVMOpcode.LLVMRet:
									if (instructionContext.Operands.Length == 1)
									{
										if (instructionContext.Operands[0].Kind == LLVMValueKind.LLVMInstructionValueKind)
										{
											CilLocalVariable returnLocal = functionContext.InstructionLocals[instructionContext.Operands[0]];
											instructions.Add(CilOpCodes.Ldloc, returnLocal);

											if (!hasReturn)
											{
												hasReturn = true;
												actualReturnType = returnLocal.VariableType;
											}
											else if (hasSingleReturn && !SignatureComparer.Default.Equals(actualReturnType, returnLocal.VariableType))
											{
												hasSingleReturn = false;
												actualReturnType = null;
											}
										}
										else
										{
											throw new NotImplementedException();
										}
									}
									else if (instructionContext.Operands.Length != 0)
									{
										throw new NotSupportedException();
									}
									else
									{
										if (!hasReturn)
										{
											hasReturn = true;

										}
										else if (hasSingleReturn && actualReturnType is not null)
										{
											hasSingleReturn = false;
											actualReturnType = null;
										}
									}

									instructions.Add(CilOpCodes.Ret);
									break;
								case LLVMOpcode.LLVMBr:
									instructionContext.AddBranchInstruction();
									break;
								case LLVMOpcode.LLVMSwitch:
									break;
								case LLVMOpcode.LLVMIndirectBr:
									break;
								case LLVMOpcode.LLVMInvoke:
									break;
								case LLVMOpcode.LLVMUnreachable:
									break;
								case LLVMOpcode.LLVMCallBr:
									break;
								case LLVMOpcode.LLVMFNeg:
									instructionContext.AddUnaryMathInstruction(CilOpCodes.Neg);
									break;
								case LLVMOpcode.LLVMAdd:
								case LLVMOpcode.LLVMFAdd:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Add);
									break;
								case LLVMOpcode.LLVMSub:
								case LLVMOpcode.LLVMFSub:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Sub);
									break;
								case LLVMOpcode.LLVMMul:
								case LLVMOpcode.LLVMFMul:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Mul);
									break;
								case LLVMOpcode.LLVMUDiv:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Div_Un);
									break;
								case LLVMOpcode.LLVMSDiv:
								case LLVMOpcode.LLVMFDiv:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Div);
									break;
								case LLVMOpcode.LLVMURem:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Rem_Un);
									break;
								case LLVMOpcode.LLVMSRem:
								case LLVMOpcode.LLVMFRem:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Rem);
									break;
								case LLVMOpcode.LLVMShl:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Shl);
									break;
								case LLVMOpcode.LLVMLShr:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Shr_Un);//Logical
									break;
								case LLVMOpcode.LLVMAShr:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Shr);//Arithmetic
									break;
								case LLVMOpcode.LLVMAnd:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.And);
									break;
								case LLVMOpcode.LLVMOr:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Or);
									break;
								case LLVMOpcode.LLVMXor:
									instructionContext.AddBinaryMathInstruction(CilOpCodes.Xor);
									break;
								case LLVMOpcode.LLVMAlloca:
									{
										Debug.Assert(instructionContext.Operands.Length == 1);
										if (instructionContext.Operands[0].Kind is not LLVMValueKind.LLVMConstantIntValueKind)
										{
											throw new NotSupportedException("Variable size alloca not supported");
										}

										LLVMTypeRef allocatedType = LLVM.GetAllocatedType(instruction);
										TypeSignature allocatedTypeSignature = moduleContext.GetTypeSignature(allocatedType);
										CilLocalVariable pointerLocal = instructions.AddLocalVariable(allocatedTypeSignature.MakePointerType());

										CilLocalVariable contentLocal;
										long size = instructionContext.Operands[0].ConstIntSExt;
										if (size != 1)
										{
											throw new NotSupportedException("Fixed size array not supported");
										}
										else
										{
											contentLocal = instructions.AddLocalVariable(allocatedTypeSignature);
											functionContext.DataLocals[pointerLocal] = contentLocal;

											//Zero out the memory
											instructions.Add(CilOpCodes.Ldloca, contentLocal);
											instructions.Add(CilOpCodes.Initobj, allocatedTypeSignature.ToTypeDefOrRef());
										}

										instructions.Add(CilOpCodes.Ldloca, contentLocal);//Might need slight modifications for fixed size arrays.
										instructions.Add(CilOpCodes.Stloc, pointerLocal);
										instructionContext.Function.InstructionLocals[instruction] = pointerLocal;
									}
									break;
								case LLVMOpcode.LLVMLoad:
									{
										Debug.Assert(instructionContext.Operands.Length == 1);
										CilLocalVariable addressLocal = functionContext.InstructionLocals[instructionContext.Operands[0]];

										//https://llvm.org/docs/OpaquePointers.html#migration-instructions
										LLVMTypeRef loadType = instruction.TypeOf;
										TypeSignature loadTypeSignature = moduleContext.GetTypeSignature(loadType);
										CilLocalVariable resultLocal = instructions.AddLocalVariable(loadTypeSignature);

										if (functionContext.DataLocals.TryGetValue(addressLocal, out CilLocalVariable? dataLocal)
											&& SignatureComparer.Default.Equals(dataLocal.VariableType, loadTypeSignature))
										{
											instructions.Add(CilOpCodes.Ldloc, dataLocal);
										}
										else
										{
											instructions.Add(CilOpCodes.Ldloc, addressLocal);
											instructions.AddLoadIndirect(loadTypeSignature);
										}
										instructions.Add(CilOpCodes.Stloc, resultLocal);

										functionContext.InstructionLocals[instruction] = resultLocal;
									}
									break;
								case LLVMOpcode.LLVMStore:
									{
										Debug.Assert(instructionContext.Operands.Length == 2);

										CilLocalVariable addressLocal = functionContext.InstructionLocals[instructionContext.Operands[1]];
										TypeSignature addressType = addressLocal.VariableType;

										//https://llvm.org/docs/OpaquePointers.html#migration-instructions
										LLVMTypeRef storeType = instructionContext.Operands[0].TypeOf;
										TypeSignature storeTypeSignature = moduleContext.GetTypeSignature(storeType);

										if (functionContext.DataLocals.TryGetValue(addressLocal, out CilLocalVariable? dataLocal)
											&& SignatureComparer.Default.Equals(dataLocal.VariableType, storeTypeSignature))
										{
											functionContext.LoadOperand(instructionContext.Operands[0]);
											instructions.Add(CilOpCodes.Stloc, dataLocal);
										}
										else
										{
											instructions.Add(CilOpCodes.Ldloc, addressLocal);
											functionContext.LoadOperand(instructionContext.Operands[0]);
											instructions.AddStoreIndirect(storeTypeSignature);
										}
										
									}
									break;
								case LLVMOpcode.LLVMGetElementPtr:
									{
										Debug.Assert(instructionContext.Operands.Length >= 2);
										LLVMTypeRef sourceElementType = LLVM.GetGEPSourceElementType(instruction);
										TypeSignature sourceElementTypeSignature = moduleContext.GetTypeSignature(sourceElementType);
										TypeSignature resultTypeSignature = sourceElementTypeSignature.MakePointerType();

										//This is the pointer. It's generally void* due to stripping.
										functionContext.LoadOperand(instructionContext.Operands[0], out _);//Pointer

										//This isn't strictly necessary, but it might make ILSpy output better someday.
										CilLocalVariable pointerLocal = instructions.AddLocalVariable(resultTypeSignature);
										functionContext.Instructions.Add(CilOpCodes.Stloc, pointerLocal);
										functionContext.Instructions.Add(CilOpCodes.Ldloc, pointerLocal);

										//This is the index, which might be a constant.
										functionContext.LoadOperand(instructionContext.Operands[1], out _);
										CilInstruction previousInstruction = functionContext.Instructions[^1];

										if (previousInstruction.IsLoadConstantInteger(out long value) && value == 0)
										{
											//Remove the Ldc_I4_0
											//There's no need to add it to the pointer.
											functionContext.Instructions.Pop();
										}
										else if (sourceElementTypeSignature.TryGetSize(out int size))
										{
											if (size == 1)
											{
												functionContext.Instructions.Add(CilOpCodes.Conv_I);
											}
											else
											{
												if (previousInstruction.IsLoadConstantInteger(out value))
												{
													previousInstruction.OpCode = CilOpCodes.Ldc_I4;
													previousInstruction.Operand = (int)(value * size);
													functionContext.Instructions.Add(CilOpCodes.Conv_I);
												}
												else
												{
													functionContext.Instructions.Add(CilOpCodes.Conv_I);
													functionContext.Instructions.Add(CilOpCodes.Ldc_I4, size);
													functionContext.Instructions.Add(CilOpCodes.Mul);
												}
											}
											functionContext.Instructions.Add(CilOpCodes.Add);
										}
										else
										{
											functionContext.Instructions.Add(CilOpCodes.Conv_I);
											functionContext.Instructions.Add(CilOpCodes.Sizeof, sourceElementTypeSignature.ToTypeDefOrRef());
											functionContext.Instructions.Add(CilOpCodes.Mul);
											functionContext.Instructions.Add(CilOpCodes.Add);
										}

										TypeSignature currentType = sourceElementTypeSignature;
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
													functionContext.Instructions.Add(CilOpCodes.Ldflda, field);
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

										CilLocalVariable resultLocal = instructions.AddLocalVariable(currentType.MakePointerType());
										functionContext.Instructions.Add(CilOpCodes.Stloc, resultLocal);
										functionContext.InstructionLocals[instruction] = resultLocal;
									}
									break;
								case LLVMOpcode.LLVMTrunc:
									break;
								case LLVMOpcode.LLVMZExt:
									break;
								case LLVMOpcode.LLVMSExt:
									{
										Debug.Assert(instructionContext.Operands.Length == 1);

										CorLibTypeSignature destinationType = (CorLibTypeSignature)instructionContext.Function.Module.GetTypeSignature(instructionContext.Instruction.TypeOf);
										CilLocalVariable resultLocal = functionContext.Instructions.AddLocalVariable(destinationType);

										instructionContext.LoadOperand(0, out TypeSignature sourceType);
										Debug.Assert(sourceType is CorLibTypeSignature);

										if (destinationType.ElementType is ElementType.I8)
										{
											instructionContext.CilInstructions.Add(CilOpCodes.Conv_I8);
										}
										else if (destinationType.ElementType is ElementType.I4)
										{
											instructionContext.CilInstructions.Add(CilOpCodes.Conv_I4);
										}
										else
										{
											throw new NotSupportedException();
										}

										instructionContext.CilInstructions.Add(CilOpCodes.Stloc, resultLocal);
										functionContext.InstructionLocals[instruction] = resultLocal;
									}
									break;
								case LLVMOpcode.LLVMFPToUI:
									break;
								case LLVMOpcode.LLVMFPToSI:
									break;
								case LLVMOpcode.LLVMUIToFP:
									break;
								case LLVMOpcode.LLVMSIToFP:
									break;
								case LLVMOpcode.LLVMFPTrunc:
									break;
								case LLVMOpcode.LLVMFPExt:
									break;
								case LLVMOpcode.LLVMPtrToInt:
									break;
								case LLVMOpcode.LLVMIntToPtr:
									break;
								case LLVMOpcode.LLVMBitCast:
									break;
								case LLVMOpcode.LLVMAddrSpaceCast:
									break;
								case LLVMOpcode.LLVMICmp:
									instructionContext.AddIntegerComparisonInstruction();
									break;
								case LLVMOpcode.LLVMFCmp:
									instructionContext.AddFloatComparisonInstruction();
									break;
								case LLVMOpcode.LLVMPHI:
									//Mostly handled by branches
									{
										Debug.Assert(instructionContext.Operands.Length > 0);
										TypeSignature phiTypeSignature = functionContext.GetOperandTypeSignature(instructionContext.Operands[0]);
										CilLocalVariable resultLocal = instructions.AddLocalVariable(phiTypeSignature);
										instructions.Add(CilOpCodes.Stloc, resultLocal);
										functionContext.InstructionLocals[instruction] = resultLocal;
									}
									break;
								case LLVMOpcode.LLVMCall:
									{
										Debug.Assert(instructionContext.Operands.Length >= 1 && instructionContext.Operands[^1].Kind == LLVMValueKind.LLVMFunctionValueKind);
										LLVMValueRef functionOperand = instructionContext.Operands[^1];
										MethodDefinition calledMethod = moduleContext.Methods[functionOperand].Definition;
										for (int i = 0; i < instructionContext.Operands.Length - 1; i++)
										{
											instructionContext.LoadOperand(i);
										}
										instructions.Add(CilOpCodes.Call, calledMethod);
										TypeSignature functionReturnType = calledMethod.Signature!.ReturnType;
										if (functionReturnType is not CorLibTypeSignature { ElementType: ElementType.Void })
										{
											CilLocalVariable resultLocal = instructions.AddLocalVariable(functionReturnType);
											instructions.Add(CilOpCodes.Stloc, resultLocal);
											functionContext.InstructionLocals[instruction] = resultLocal;
										}
									}
									break;
								case LLVMOpcode.LLVMSelect:
									break;
								case LLVMOpcode.LLVMUserOp1:
									break;
								case LLVMOpcode.LLVMUserOp2:
									break;
								case LLVMOpcode.LLVMVAArg:
									break;
								case LLVMOpcode.LLVMExtractElement:
									break;
								case LLVMOpcode.LLVMInsertElement:
									break;
								case LLVMOpcode.LLVMShuffleVector:
									break;
								case LLVMOpcode.LLVMExtractValue:
									break;
								case LLVMOpcode.LLVMInsertValue:
									break;
								case LLVMOpcode.LLVMFreeze:
									break;
								case LLVMOpcode.LLVMFence:
									break;
								case LLVMOpcode.LLVMAtomicCmpXchg:
									break;
								case LLVMOpcode.LLVMAtomicRMW:
									break;
								case LLVMOpcode.LLVMResume:
									break;
								case LLVMOpcode.LLVMLandingPad:
									break;
								case LLVMOpcode.LLVMCleanupRet:
									break;
								case LLVMOpcode.LLVMCatchRet:
									break;
								case LLVMOpcode.LLVMCatchPad:
									break;
								case LLVMOpcode.LLVMCleanupPad:
									break;
								case LLVMOpcode.LLVMCatchSwitch:
									break;
							}
						}
					}

					if (actualReturnType is not null && !SignatureComparer.Default.Equals(actualReturnType, method.Signature!.ReturnType))
					{
						method.Signature.ReturnType = actualReturnType;
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

	private static string Demangle(string name)
	{
		if (name.StartsWith('?'))
		{
			int start = name.StartsWith("??$") ? 3 : 1;
			int end = name.IndexOf('@', start);
			return name[start..end];
		}
		else
		{
			return name;
		}
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
