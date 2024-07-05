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

	public static void Translate(string inputPath, string outputPath)
	{
		ModuleDefinition moduleDefinition = new("ConvertedCpp");
		LLVMMemoryBufferRef buffer = LoadIR(inputPath);
		try
		{
			LLVMContextRef context = LLVMContextRef.Create();
			try
			{
				LLVMModuleRef module = context.ParseIR(buffer);
				TypeDefinition typeDefinition = new(null, "GlobalMembers", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
				moduleDefinition.TopLevelTypes.Add(typeDefinition);

				ModuleContext moduleContext = new(module, moduleDefinition);
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

					Dictionary<LLVMValueRef, Parameter> parameters = new();
					foreach (LLVMValueRef parameter in function.GetParams())
					{
						LLVMTypeRef type = parameter.TypeOf;
						TypeSignature parameterType = moduleContext.GetTypeSignature(type);
						parameters[parameter] = method.AddParameter(parameterType);
					}

					method.CilMethodBody = new(method);

					CilInstructionCollection instructions = method.CilMethodBody.Instructions;

					foreach (LLVMBasicBlockRef basicBlock in function.GetBasicBlocks())
					{
						functionContext.Labels[basicBlock] = new();
					}

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
											instructions.Add(CilOpCodes.Ldloc, functionContext.Locals[instructionContext.Operands[0]]);
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
										Debug.Assert(instructionContext.Operands.Length == 1 && instructionContext.Operands[0].Kind is LLVMValueKind.LLVMConstantIntValueKind);
										long size = instructionContext.Operands[0].ConstIntSExt;
										Debug.Assert(size == 1);
										LLVMTypeRef allocatedType = LLVM.GetAllocatedType(instruction);
										TypeSignature allocatedTypeSignature = moduleContext.GetTypeSignature(allocatedType);
										instructionContext.Function.Locals[instruction] = instructions.AddLocalVariable(allocatedTypeSignature);
									}
									break;
								case LLVMOpcode.LLVMLoad:
									{
										Debug.Assert(instructionContext.Operands.Length == 1);
										CilLocalVariable addressLocal = functionContext.Locals[instructionContext.Operands[0]];
										CilLocalVariable resultLocal = instructions.AddLocalVariable(addressLocal.VariableType);
										instructions.Add(CilOpCodes.Ldloc, addressLocal);
										instructions.Add(CilOpCodes.Stloc, resultLocal);
										functionContext.Locals[instruction] = resultLocal;
									}
									break;
								case LLVMOpcode.LLVMStore:
									{
										Debug.Assert(instructionContext.Operands.Length == 2);
										switch (instructionContext.Operands[0].Kind)
										{
											case LLVMValueKind.LLVMInstructionValueKind:
												{
													CilLocalVariable valueLocal = functionContext.Locals[instructionContext.Operands[0]];
													instructions.Add(CilOpCodes.Ldloc, valueLocal);
												}
												break;
											case LLVMValueKind.LLVMArgumentValueKind:
												{
													Parameter parameter = parameters[instructionContext.Operands[0]];
													instructions.Add(CilOpCodes.Ldarg, parameter);
												}
												break;
											default:
												throw new NotSupportedException();
										}
										CilLocalVariable addressLocal = functionContext.Locals[instructionContext.Operands[1]];
										instructions.Add(CilOpCodes.Stloc, addressLocal);
									}
									break;
								case LLVMOpcode.LLVMGetElementPtr:
									break;
								case LLVMOpcode.LLVMTrunc:
									break;
								case LLVMOpcode.LLVMZExt:
									break;
								case LLVMOpcode.LLVMSExt:
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
								case LLVMOpcode.LLVMFCmp:
									instructionContext.AddComparisonInstruction();
									break;
								case LLVMOpcode.LLVMPHI:
									//Mostly handled by branches
									{
										Debug.Assert(instructionContext.Operands.Length > 0);
										TypeSignature phiTypeSignature = functionContext.GetOperandTypeSignature(instructionContext.Operands[0]);
										CilLocalVariable resultLocal = instructions.AddLocalVariable(phiTypeSignature);
										instructions.Add(CilOpCodes.Stloc, resultLocal);
										functionContext.Locals[instruction] = resultLocal;
									}
									break;
								case LLVMOpcode.LLVMCall:
									{
										Debug.Assert(instructionContext.Operands.Length >= 1 && instructionContext.Operands[^1].Kind == LLVMValueKind.LLVMFunctionValueKind);
										LLVMValueRef functionOperand = instructionContext.Operands[^1];
										MethodDefinition calledMethod = moduleContext.Methods[functionOperand].Definition;
										for (int i = 0; i < instructionContext.Operands.Length - 1; i++)
										{
											CilLocalVariable parameterLocal = functionContext.Locals[instructionContext.Operands[i]];
											instructions.Add(CilOpCodes.Ldloc, parameterLocal);
										}
										instructions.Add(CilOpCodes.Call, calledMethod);
										TypeSignature functionReturnType = calledMethod.Signature!.ReturnType;
										if (functionReturnType is not CorLibTypeSignature { ElementType: ElementType.Void })
										{
											CilLocalVariable resultLocal = instructions.AddLocalVariable(functionReturnType);
											instructions.Add(CilOpCodes.Stloc, resultLocal);
											functionContext.Locals[instruction] = resultLocal;
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
