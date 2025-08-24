using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.Translation.LlvmIR.Attributes;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Variables;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal unsafe readonly struct InstructionLifter
{
	private readonly Dictionary<LLVMBasicBlockRef, BasicBlock> basicBlocks = new();
	private readonly Dictionary<BasicBlock, LLVMBasicBlockRef> basicBlockRefs = new();
	/// <summary>
	/// This can include generated blocks that are not part of the original IL.
	/// </summary>
	private readonly List<BasicBlock> basicBlockList = new();
	private readonly Dictionary<LLVMValueRef, IVariable> instructionResults = new();
	private readonly Dictionary<LLVMValueRef, IVariable> allocaData = new();
	private readonly FunctionContext? function;
	private readonly ModuleContext module;

	private InstructionLifter(FunctionContext? function, ModuleContext module)
	{
		this.function = function;
		this.module = module;
	}

	public static unsafe IReadOnlyList<BasicBlock> Lift(FunctionContext function)
	{
		InstructionLifter lifter = new(function, function.Module);

		LLVMBasicBlockRef[] blockRefs = function.Function.GetBasicBlocks();

		lifter.basicBlocks.EnsureCapacity(blockRefs.Length);
		lifter.basicBlockRefs.EnsureCapacity(blockRefs.Length);
		lifter.basicBlockList.EnsureCapacity(blockRefs.Length);

		for (int i = 0; i < blockRefs.Length; i++)
		{
			BasicBlock basicBlock = new();
			LLVMBasicBlockRef basicBlockRef = blockRefs[i];
			lifter.basicBlocks[basicBlockRef] = basicBlock;
			lifter.basicBlockRefs[basicBlock] = basicBlockRef;
			lifter.basicBlockList.Add(basicBlock);
		}

		foreach (LLVMValueRef instruction in function.Function.GetInstructions())
		{
			if (instruction.InstructionOpcode is LLVMOpcode.LLVMAlloca)
			{
				TypeSignature allocatedType = lifter.GetTypeSignature(LLVM.GetAllocatedType(instruction));
				LLVMValueRef sizeOperand = instruction.GetOperand(0);
				long fixedSize = sizeOperand.ConstIntSExt;
				TypeSignature dataType = fixedSize != 1
					? lifter.module.GetOrCreateInlineArray(allocatedType, (int)fixedSize).Type.ToTypeSignature()
					: allocatedType;

				IVariable dataLocal = function is not null && function.MightThrowAnException ? new FunctionFieldVariable(dataType, function) : new LocalVariable(dataType);

				lifter.allocaData.Add(instruction, dataLocal);

				continue;
			}

			TypeSignature resultType = lifter.GetTypeSignature(instruction);
			if (resultType is not CorLibTypeSignature { ElementType: ElementType.Void })
			{
				lifter.instructionResults[instruction] = new LocalVariable(resultType);
			}
		}

		foreach ((LLVMBasicBlockRef blockRef, BasicBlock basicBlock) in lifter.basicBlocks)
		{
			foreach (LLVMValueRef instruction in blockRef.GetInstructions())
			{
				lifter.AddInstruction(basicBlock, instruction);
			}
		}

		return lifter.basicBlockList;
	}

	public static BasicBlock Initialize(GlobalVariableContext globalVariable)
	{
		Debug.Assert(globalVariable.HasSingleOperand);

		InstructionLifter lifter = new(null, globalVariable.Module);

		BasicBlock instructions = new();
		lifter.LoadValue(instructions, globalVariable.Operand);
		Call(instructions, globalVariable.DataSetMethod);

		return instructions;
	}

	private void AddInstruction(BasicBlock basicBlock, LLVMValueRef instruction)
	{
		if (TryMatchImageOffset(instruction, module, out FunctionContext? function2, out GlobalVariableContext? variable2))
		{
			MethodDefinition getIndexMethod = module.InjectedTypes[typeof(PointerIndices)].GetMethodByName(nameof(PointerIndices.GetIndex));

			if (function2 is not null)
			{
				LoadVariable(basicBlock, new FunctionPointerVariable(function2));
			}
			else if (variable2 is not null)
			{
				basicBlock.Add(new AddressOfInstruction(variable2));
			}
			else
			{
				Debug.Fail("This should be unreachable.");
			}
			Call(basicBlock, getIndexMethod);
			StoreResult(basicBlock, instruction);
			return;
		}

		LLVMValueRef[] operands = instruction.GetOperands();
		LLVMOpcode opcode = instruction.GetOpcode();
		switch (opcode)
		{
			case LLVMOpcode.LLVMAlloca:
				{
					basicBlock.Add(new InitializeInstruction(allocaData[instruction]));
				}
				break;
			case LLVMOpcode.LLVMLoad:
				{
					Debug.Assert(operands.Length == 1, "Load instruction should have exactly one operand");

					LLVMValueRef sourceOperand = operands[0];
					LoadValue(basicBlock, sourceOperand);

					TypeSignature type = module.GetTypeSignature(instruction);
					basicBlock.Add(new LoadIndirectInstruction(type));

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMStore:
				{
					Debug.Assert(operands.Length == 2, "Store instruction should have exactly two operands");

					LLVMValueRef valueOperand = operands[0];
					LLVMValueRef pointerOperand = operands[1];

					LoadValue(basicBlock, pointerOperand);
					LoadValue(basicBlock, valueOperand);

					TypeSignature type = module.GetTypeSignature(valueOperand);
					basicBlock.Add(new StoreIndirectInstruction(type));
				}
				break;
			case LLVMOpcode.LLVMRet:
				{
					Debug.Assert(operands.Length <= 1, "Return instruction should have at most one operand");
					if (operands.Length is 1)
					{
						LoadValue(basicBlock, operands[0]);
					}
					if (function is { NeedsStackFrame: true })
					{
						basicBlock.Add(new ClearStackFrameInstruction(function));
					}
					basicBlock.Add(operands.Length == 0 ? ReturnInstruction.Void : ReturnInstruction.Value);
				}
				break;
			case LLVMOpcode.LLVMBr:
				{
					if (operands.Length == 1)
					{
						LLVMBasicBlockRef targetBlockRef = operands[0].AsBasicBlock();

						Branch(basicBlock, targetBlockRef);
					}
					else if (operands.Length == 3)
					{
						Debug.Assert(operands[0].IsInstruction() || operands[0].IsConstant);
						Debug.Assert(operands[0] == instruction.Condition);
						Debug.Assert(operands[1].IsBasicBlock);
						Debug.Assert(operands[2].IsBasicBlock);

						LLVMValueRef condition = operands[0];

						// I have no idea why, but the second and third operands seem to be swapped.
						LLVMBasicBlockRef trueBlock = operands[2].AsBasicBlock();
						LLVMBasicBlockRef falseBlock = operands[1].AsBasicBlock();

						LoadValue(basicBlock, condition);

						ConditionalBranch(basicBlock, trueBlock, falseBlock);
					}
					else
					{
						throw new NotSupportedException($"Unsupported branch instruction with {operands.Length} operands");
					}
				}
				break;
			case LLVMOpcode.LLVMSwitch:
				{
					Debug.Assert(operands.Length >= 2);
					Debug.Assert(operands.Length % 2 == 0);

					LLVMValueRef indexOperand = operands[0];
					LLVMBasicBlockRef defaultBlockRef = operands[1].AsBasicBlock();
					ReadOnlySpan<(LLVMValueRef Case, LLVMValueRef Target)> cases = MemoryMarshal.Cast<LLVMValueRef, (LLVMValueRef Case, LLVMValueRef Target)>(operands.AsSpan(2));

					LoadValue(basicBlock, indexOperand);

					(long value, BasicBlock target)[] caseTargets = new (long, BasicBlock)[cases.Length];
					for (int i = 0; i < cases.Length; i++)
					{
						LLVMValueRef caseValue = cases[i].Case;
						if (caseValue.IsAConstantInt == default)
						{
							throw new NotSupportedException();
						}
						caseValue.TypeOf.ThrowIfNotCoreLibInteger();
						LLVMBasicBlockRef targetBlockRef = cases[i].Target.AsBasicBlock();
						BasicBlock targetBlock;
						if (targetBlockRef.StartsWithPhi())
						{
							BasicBlock helperBasicBlock = new();
							basicBlockList.Add(helperBasicBlock);

							Branch(helperBasicBlock, basicBlockRefs[basicBlock], targetBlockRef);

							targetBlock = helperBasicBlock;
						}
						else
						{
							targetBlock = basicBlocks[targetBlockRef];
						}
						caseTargets[i] = (caseValue.ConstIntSExt, targetBlock);
					}

					TypeSignature indexType = module.GetTypeSignature(indexOperand);
					basicBlock.Add(new SwitchInstruction(indexType, caseTargets));

					Branch(basicBlock, defaultBlockRef);
				}
				break;
			case LLVMOpcode.LLVMSelect:
				{
					Debug.Assert(operands.Length == 3, "Select instruction should have exactly three operands");

					LLVMValueRef condition = operands[0];
					LLVMValueRef trueValue = operands[1];
					LLVMValueRef falseValue = operands[2];

					if (module.GetTypeSignature(condition) is not CorLibTypeSignature { ElementType: ElementType.Boolean })
					{
						throw new NotImplementedException("Non-boolean condition for select instructions");
					}

					LoadValue(basicBlock, condition);

					LoadValue(basicBlock, trueValue);

					LoadValue(basicBlock, falseValue);

					if (module.GetTypeSignature(condition) is CorLibTypeSignature { ElementType: ElementType.Boolean })
					{
						TypeSignature valueTypeSignature = module.GetTypeSignature(trueValue);

						if (valueTypeSignature is PointerTypeSignature)
						{
							IMethodDescriptor helperMethod = module.InstructionHelperType.Methods
								.Single(m => m.Name == nameof(InstructionHelper.Select) && m.GenericParameters.Count is 0);

							Call(basicBlock, helperMethod);
						}
						else
						{
							IMethodDescriptor helperMethod = module.InstructionHelperType.Methods
								.Single(m => m.Name == nameof(InstructionHelper.Select) && m.GenericParameters.Count is 1)
								.MakeGenericInstanceMethod(valueTypeSignature);

							Call(basicBlock, helperMethod);
						}
					}
					else
					{
						throw new NotImplementedException("Non-boolean condition for select instructions");
					}

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMPHI:
				{
					Debug.Assert(operands.Length > 0, "Phi instruction should have at least one operand");

					basicBlock.Add(PhiPushInstruction.Instance);

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMUnreachable:
				{
					basicBlock.Add(UnreachableInstruction.Instance);
				}
				break;
			case LLVMOpcode.LLVMICmp:
				{
					Debug.Assert(operands.Length == 2);

					LoadValue(basicBlock, operands[0]);
					LoadValue(basicBlock, operands[1]);

					TypeSignature type = module.GetTypeSignature(operands[0]);
					basicBlock.Add(NumericalComparison.Create(type, instruction.ICmpPredicate));

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMFCmp:
				{
					Debug.Assert(operands.Length == 2);

					LoadValue(basicBlock, operands[0]);
					LoadValue(basicBlock, operands[1]);

					TypeSignature type = module.GetTypeSignature(operands[0]);
					basicBlock.Add(NumericalComparison.Create(type, instruction.FCmpPredicate));

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMFNeg:
				{
					Debug.Assert(operands.Length == 1, "Unary negation instruction should have exactly one operand");

					LoadValue(basicBlock, operands[0]);

					TypeSignature type = module.GetTypeSignature(instruction);
					basicBlock.Add(NegationInstruction.Create(type, module));

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMBitCast:
				{
					Debug.Assert(operands.Length == 1, "BitCast instruction should have exactly one operand");

					TypeSignature sourceType = module.GetTypeSignature(operands[0]);
					TypeSignature resultType = module.GetTypeSignature(instruction);

					IMethodDescriptor method = module.InstructionHelperType.Methods
						.First(m => m.Name == nameof(InstructionHelper.BitCast))
						.MakeGenericInstanceMethod(sourceType, resultType);

					LoadValue(basicBlock, operands[0]);
					Call(basicBlock, method);
				}
				break;
			case LLVMOpcode.LLVMVAArg:
				{
					// On Windows, this doesn't get used because Clang optimizes va_list away.

					Debug.Assert(operands.Length == 1);

					LoadValue(basicBlock, operands[0]);

					Call(basicBlock, module.InstructionHelperType.Methods.First(m => m.Name == nameof(InstructionHelper.VAArg)));

					TypeSignature resultType = module.GetTypeSignature(instruction);
					basicBlock.Add(new LoadIndirectInstruction(resultType));

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMCatchSwitch:
				{
					Debug.Assert(operands.Length >= 1, "Catch switch instruction should have at least one operand");

					Debug.Assert(function is not null);
					Debug.Assert(function.PersonalityFunction is not null);
					Debug.Assert(function.PersonalityFunction.IsIntrinsic, "Personality function should be intrinsic and not have instructions");
					Debug.Assert(function.PersonalityFunction.ReturnTypeSignature is CorLibTypeSignature { ElementType: ElementType.I4 });
					Debug.Assert(function.PersonalityFunction.NormalParameters.Length == 0);
					Debug.Assert(function.PersonalityFunction.IsVariadic);

					// The first operand is the parent catch switch

					bool hasDefaultUnwind = false; // Default unwind detection is not implemented yet.

					LLVMBasicBlockRef defaultUnwindTargetRef = hasDefaultUnwind
						? operands[^1].AsBasicBlock()
						: default;
					ReadOnlySpan<LLVMValueRef> handlerBlocks = hasDefaultUnwind
						? operands.AsSpan(1, operands.Length - 2)
						: operands.AsSpan(1);
					LLVMValueRef[] catchPads = new LLVMValueRef[handlerBlocks.Length];
					for (int i = 0; i < handlerBlocks.Length; i++)
					{
						LLVMBasicBlockRef handlerBlock = handlerBlocks[i].AsBasicBlock();
						catchPads[i] = handlerBlock.GetInstructions().First(static i => i.IsACatchPadInst != default);
					}
					Debug.Assert(catchPads.Length > 0, "Catch switch instruction should have at least one catch pad");

					// Catch pads
					for (int i = 0; i < catchPads.Length; i++)
					{
						LLVMValueRef catchPad = catchPads[i];
						ReadOnlySpan<LLVMValueRef> catchPadArguments = catchPad.GetOperands().AsSpan()[..^1]; // The last operand is the catch switch
						LLVMBasicBlockRef catchPadBasicBlockRef = catchPad.InstructionParent;

						IVariable argumentsInReadOnlySpan = LoadVariadicArguments(basicBlock, catchPadArguments, module);

						// Call personality function
						basicBlock.Add(new LoadVariableInstruction(argumentsInReadOnlySpan));
						Call(basicBlock, function.PersonalityFunction.Definition);

						BasicBlock targetBlock;
						if (catchPadBasicBlockRef.StartsWithPhi())
						{
							BasicBlock helperBasicBlock = new();
							basicBlockList.Add(helperBasicBlock);

							Branch(helperBasicBlock, basicBlockRefs[basicBlock], catchPadBasicBlockRef);

							targetBlock = helperBasicBlock;
						}
						else
						{
							targetBlock = basicBlocks[catchPadBasicBlockRef];
						}

						basicBlock.Add(new BranchIfFalseInstruction(targetBlock));
					}

					if (hasDefaultUnwind)
					{
						Branch(basicBlock, defaultUnwindTargetRef);
					}
					else
					{
						// Unwind to caller
						basicBlock.Add(new ReturnDefaultInstruction(function.Definition.Signature!.ReturnType));
					}
				}
				break;
			case LLVMOpcode.LLVMCatchPad:
			case LLVMOpcode.LLVMCleanupPad:
				{
					FieldDefinition exceptionInfoField = module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current));

					// Store the current exception info in a local variable
					basicBlock.Add(new LoadFieldInstruction(exceptionInfoField));
					StoreResult(basicBlock, instruction);

					// Set the current exception info to null
					LoadVariable(basicBlock, new DefaultVariable(exceptionInfoField.Signature!.FieldType));
					basicBlock.Add(new StoreFieldInstruction(exceptionInfoField));
				}
				break;
			case LLVMOpcode.LLVMCatchRet:
				{
					Debug.Assert(operands.Length == 2, "Catch return instruction should have exactly two operands");
					Debug.Assert(operands[0].IsACatchPadInst != default, "First operand of catch return instruction should be a catch pad");
					Debug.Assert(operands[1].IsBasicBlock, "Second operand of catch return instruction should be a basic block");

					LLVMValueRef catchPad = operands[0];
					LLVMBasicBlockRef targetBlockRef = operands[1].AsBasicBlock();

					LoadValue(basicBlock, catchPad);

					Call(basicBlock, module.InjectedTypes[typeof(ExceptionInfo)].Methods.Single(m => m.Name == nameof(ExceptionInfo.Dispose) && m.IsPublic));

					Branch(basicBlock, targetBlockRef);
				}
				break;
			case LLVMOpcode.LLVMCleanupRet:
				{
					Debug.Assert(function is not null);
					Debug.Assert(operands.Length is 1 or 2, "Cleanup return instruction should have one or two operands");

					LLVMValueRef cleanupPad = operands[0];
					bool unwindsToCaller = operands.Length == 1;

					FieldDefinition exceptionInfoField = module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current));

					// Restore the current exception info from the cleanup pad
					LoadValue(basicBlock, cleanupPad);
					basicBlock.Add(new StoreFieldInstruction(exceptionInfoField));

					if (unwindsToCaller)
					{
						basicBlock.Add(new ReturnDefaultInstruction(function.Definition.Signature!.ReturnType));
					}
					else
					{
						// Unwind to an exception handler switch or another cleanup pad
						Branch(basicBlock, operands[1].AsBasicBlock());
					}
				}
				break;
			case LLVMOpcode.LLVMCall:
			case LLVMOpcode.LLVMInvoke:
				{
					Debug.Assert(function is not null);

					LLVMValueRef functionOperand = operands[^1];
					FunctionContext? functionCalled = module.Methods.TryGetValue(functionOperand);

					LLVMTypeRef calledFunctionType = LLVM.GetCalledFunctionType(instruction);
					int argumentCount = (int)calledFunctionType.ParamTypesCount;
					ReadOnlySpan<LLVMValueRef> argumentOperands = operands.AsSpan(0, argumentCount);

					if (functionCalled is null)
					{
						foreach (LLVMValueRef argumentOperand in argumentOperands)
						{
							LoadValue(basicBlock, argumentOperand);
						}

						TypeSignature[] parameterTypes = new TypeSignature[argumentOperands.Length];
						for (int i = 0; i < argumentOperands.Length; i++)
						{
							LLVMValueRef operand = argumentOperands[i];
							parameterTypes[i] = module.GetTypeSignature(operand);
						}
						TypeSignature resultTypeSignature = module.GetTypeSignature(instruction);
						MethodSignature methodSignature = MethodSignature.CreateStatic(resultTypeSignature, parameterTypes);

						if (functionOperand.Kind is LLVMValueKind.LLVMInlineAsmValueKind)
						{
							LLVMInlineAsmDialect dialect = LLVM.GetInlineAsmDialect(functionOperand);
							LLVMTypeRef inlineAssemblyFunctionType = LLVM.GetInlineAsmFunctionType(functionOperand);
							Debug.Assert(calledFunctionType == inlineAssemblyFunctionType);
							int canUnwind = LLVM.GetInlineAsmCanUnwind(functionOperand);
							string assemblyString;
							{
								nuint length = 0;
								sbyte* assemblyStringPtr = LLVM.GetInlineAsmAsmString(functionOperand, &length);
								assemblyString = Marshal.PtrToStringAnsi((nint)assemblyStringPtr, (int)length) ?? string.Empty;
							}
							string constraintString;
							{
								nuint length = 0;
								sbyte* constraintStringPtr = LLVM.GetInlineAsmConstraintString(functionOperand, &length);
								constraintString = Marshal.PtrToStringAnsi((nint)constraintStringPtr, (int)length) ?? string.Empty;
							}

							TypeDefinition declaringType = module.InjectedTypes[typeof(AssemblyFunctions)];
							MethodDefinition method = new($"M{declaringType.Methods.Count}", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, methodSignature);
							declaringType.Methods.Add(method);

							method.CilMethodBody = new(method);
							method.CilMethodBody.Instructions.Add(CilOpCodes.Ldnull);
							method.CilMethodBody.Instructions.Add(CilOpCodes.Throw);

							// Attribute
							{
								MethodDefinition constructor = module.InjectedTypes[typeof(InlineAssemblyAttribute)].GetMethodByName(".ctor");
								CustomAttributeSignature signature = new();
								signature.FixedArguments.Add(new(module.Definition.CorLibTypeFactory.String, assemblyString));
								signature.FixedArguments.Add(new(module.Definition.CorLibTypeFactory.String, constraintString));
								CustomAttribute attribute = new(constructor, signature);
								method.CustomAttributes.Add(attribute);
							}

							Call(basicBlock, method);
						}
						else
						{
							LoadValue(basicBlock, functionOperand);

							basicBlock.Add(new CallIndirectInstruction(methodSignature));
						}
					}
					else if (IsInvisibleFunction(functionCalled))
					{
						Debug.Assert(functionCalled.IsVoidReturn, "Invisible function should have a void return type");
					}
					else if (functionCalled.MangledName is "llvm.va_start.p0")
					{
						Debug.Assert(function is not null);
						Debug.Assert(function.VariadicParameter is not null);
						Debug.Assert(functionCalled.IsVoidReturn && functionCalled.NormalParameters.Length is 1 && functionCalled.VariadicParameter is null, "VA start function should have one parameter and a void return type");
						Debug.Assert(argumentOperands.Length == 1, "VA start function should have one argument");

						LoadValue(basicBlock, argumentOperands[0]);

						basicBlock.Add(new LoadVariableInstruction(function.VariadicParameter));

						Call(basicBlock, module.InstructionHelperType.GetMethodByName(nameof(InstructionHelper.VAStart)));
					}
					else
					{
						int variadicParameterCount = argumentOperands.Length - functionCalled.NormalParameters.Length;

						if (!functionCalled.IsVariadic)
						{
							Debug.Assert(variadicParameterCount == 0, "Function should not have variadic parameters");

							foreach (LLVMValueRef argumentOperand in argumentOperands)
							{
								LoadValue(basicBlock, argumentOperand);
							}
						}
						else if (variadicParameterCount == 0)
						{
							for (int i = 0; i < functionCalled.NormalParameters.Length; i++)
							{
								LoadValue(basicBlock, argumentOperands[i]);
							}
							TypeSignature variadicArrayType = functionCalled.Definition.Signature!.ParameterTypes[^1];
							basicBlock.Add(new LoadVariableInstruction(new DefaultVariable(variadicArrayType)));
						}
						else
						{
							IVariable intPtrReadOnlySpanLocal = LoadVariadicArguments(basicBlock, argumentOperands[functionCalled.NormalParameters.Length..], module);

							// Push the arguments onto the stack
							for (int i = 0; i < functionCalled.NormalParameters.Length; i++)
							{
								LoadValue(basicBlock, argumentOperands[i]);
							}
							basicBlock.Add(new LoadVariableInstruction(intPtrReadOnlySpanLocal));
						}

						Call(basicBlock, functionCalled.Definition);
					}

					MaybeStoreResult(basicBlock, instruction);

					if (opcode is LLVMOpcode.LLVMCall)
					{
						if (functionCalled is null or { MightThrowAnException: true } && function.MightThrowAnException)
						{
							basicBlock.Add(ReturnIfExceptionInfoNotNullInstruction.Create(function.Definition.Signature!.ReturnType, module));
						}
					}
					else if (opcode is LLVMOpcode.LLVMInvoke)
					{
						LLVMBasicBlockRef catchBlockRef = operands[^2].AsBasicBlock();
						LLVMBasicBlockRef defaultBlockRef = operands[^3].AsBasicBlock();

						basicBlock.Add(new LoadFieldInstruction(module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current))));
						ConditionalBranch(basicBlock, catchBlockRef, defaultBlockRef);
					}

					static bool IsInvisibleFunction(FunctionContext functionCalled)
					{
						return functionCalled.MangledName is "llvm.va_end.p0";
					}
				}
				break;
			case LLVMOpcode.LLVMGetElementPtr:
				{
					Debug.Assert(operands.Length >= 2, "GetElementPtr instruction should have at least two operands");

					LLVMValueRef source = operands[0];
					LLVMValueRef initialIndex = operands[1];
					ReadOnlySpan<LLVMValueRef> otherIndices = operands.AsSpan(2);

					LLVMTypeRef sourceElementType = LLVM.GetGEPSourceElementType(instruction);
					TypeSignature sourceElementTypeSignature = module.GetTypeSignature(sourceElementType);

					LoadValue(basicBlock, source);
					LoadArrayOffset(basicBlock, initialIndex, sourceElementTypeSignature);

					TypeSignature currentType = sourceElementTypeSignature;
					foreach (LLVMValueRef operand in otherIndices)
					{
						LLVMTypeRef operandType = operand.TypeOf;

						operandType.ThrowIfNotCoreLibInteger();

						TypeDefOrRefSignature structTypeSignature = (TypeDefOrRefSignature)currentType;
						TypeDefinition structType = (TypeDefinition)structTypeSignature.ToTypeDefOrRef();

						if (module.InlineArrayTypes.TryGetValue(structType, out InlineArrayContext? inlineArray))
						{
							LoadArrayOffset(basicBlock, operand, inlineArray.ElementType);
							currentType = inlineArray.ElementType;
						}
						else
						{
							Debug.Assert(operand.Kind == LLVMValueKind.LLVMConstantIntValueKind);

							int index = (int)operand.ConstIntSExt;
							FieldDefinition field = structType.GetInstanceField(index);
							basicBlock.Add(new LoadFieldAddressInstruction(field));
							currentType = field.Signature!.FieldType;
						}
					}

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMExtractValue:
				{
					Debug.Assert(operands.Length is 1);
					LLVMValueRef source = operands[0];
					
					ReadOnlySpan<uint> indices = new(LLVM.GetIndices(instruction), (int)LLVM.GetNumIndices(instruction));

					LoadValue(basicBlock, source);

					TypeSignature sourceType = module.GetTypeSignature(source);
					LocalVariable sourceLocal = new(sourceType);
					basicBlock.Add(new StoreVariableInstruction(sourceLocal));

					basicBlock.Add(new AddressOfInstruction(sourceLocal));
					TypeSignature currentType = sourceType;
					foreach (uint index in indices)
					{
						TypeDefOrRefSignature structTypeSignature = (TypeDefOrRefSignature)currentType;
						TypeDefinition structType = (TypeDefinition)structTypeSignature.ToTypeDefOrRef();

						if (module.InlineArrayTypes.TryGetValue(structType, out InlineArrayContext? inlineArray))
						{
							LoadArrayOffset(basicBlock, (int)index, inlineArray.ElementType);
							currentType = inlineArray.ElementType;
						}
						else
						{
							FieldDefinition field = structType.GetInstanceField((int)index);
							basicBlock.Add(new LoadFieldAddressInstruction(field));
							currentType = field.Signature!.FieldType;
						}
					}
					basicBlock.Add(new LoadIndirectInstruction(currentType));

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMInsertValue:
				{
					Debug.Assert(operands.Length is 2);
					LLVMValueRef source = operands[0];
					LLVMValueRef value = operands[1];

					ReadOnlySpan<uint> indices = new(LLVM.GetIndices(instruction), (int)LLVM.GetNumIndices(instruction));

					LoadValue(basicBlock, source);

					TypeSignature sourceType = module.GetTypeSignature(source);
					LocalVariable sourceLocal = new(sourceType);
					basicBlock.Add(new StoreVariableInstruction(sourceLocal));

					basicBlock.Add(new AddressOfInstruction(sourceLocal));
					TypeSignature currentType = sourceType;
					foreach (uint index in indices)
					{
						TypeDefOrRefSignature structTypeSignature = (TypeDefOrRefSignature)currentType;
						TypeDefinition structType = (TypeDefinition)structTypeSignature.ToTypeDefOrRef();

						if (module.InlineArrayTypes.TryGetValue(structType, out InlineArrayContext? inlineArray))
						{
							LoadArrayOffset(basicBlock, (int)index, inlineArray.ElementType);
							currentType = inlineArray.ElementType;
						}
						else
						{
							FieldDefinition field = structType.GetInstanceField((int)index);
							basicBlock.Add(new LoadFieldAddressInstruction(field));
							currentType = field.Signature!.FieldType;
						}
					}

					LoadValue(basicBlock, value);

					basicBlock.Add(new StoreIndirectInstruction(currentType));

					LoadVariable(basicBlock, sourceLocal);
					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMExtractElement:
				{
					Debug.Assert(operands.Length == 2, "ExtractElement instruction should have exactly two operands");
					LLVMValueRef vectorOperand = operands[0];
					LLVMValueRef indexOperand = operands[1];
					TypeSignature arrayType = module.GetTypeSignature(vectorOperand);
					TypeSignature elementType = module.InlineArrayTypes[(TypeDefinition)arrayType.ToTypeDefOrRef()].ElementType;
					TypeSignature indexType = module.GetTypeSignature(indexOperand);

					LoadValue(basicBlock, vectorOperand);
					LoadValue(basicBlock, indexOperand);
					if (indexType is not CorLibTypeSignature)
					{
						throw new NotSupportedException();
					}
					else if (indexType.ElementType is ElementType.I8 or ElementType.U8)
					{
						basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Conv_I4));
					}

					IMethodDescriptor method = module.InstructionHelperType.Methods
						.First(m => m.Name == nameof(InstructionHelper.ExtractElement))
						.MakeGenericInstanceMethod(arrayType, elementType);
					Call(basicBlock, method);

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMInsertElement:
				{
					Debug.Assert(operands.Length == 3);
					LLVMValueRef vectorOperand = operands[0];
					LLVMValueRef valueOperand = operands[1];
					LLVMValueRef indexOperand = operands[2];

					TypeSignature arrayType = module.GetTypeSignature(vectorOperand);
					TypeSignature elementType = module.InlineArrayTypes[(TypeDefinition)arrayType.ToTypeDefOrRef()].ElementType;
					TypeSignature indexType = module.GetTypeSignature(indexOperand);
					Debug.Assert(SignatureComparer.Default.Equals(elementType, module.GetTypeSignature(valueOperand)), "Value operand should have the same type as the array element type");

					LoadValue(basicBlock, vectorOperand);
					LoadValue(basicBlock, valueOperand);
					LoadValue(basicBlock, indexOperand);
					if (indexType is not CorLibTypeSignature)
					{
						throw new NotSupportedException();
					}
					else if (indexType.ElementType is ElementType.I8 or ElementType.U8)
					{
						basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Conv_I4));
					}

					IMethodDescriptor method = module.InstructionHelperType.Methods
						.First(m => m.Name == nameof(InstructionHelper.InsertElement))
						.MakeGenericInstanceMethod(arrayType, elementType);
					Call(basicBlock, method);

					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMShuffleVector:
				{
					Debug.Assert(operands.Length == 3);
					LLVMValueRef vector1Operand = operands[0];
					LLVMValueRef vector2Operand = operands[1];
					LLVMValueRef maskOperand = operands[2];

					TypeSignature vectorArrayType = module.GetTypeSignature(vector1Operand);
					TypeSignature vectorElementType = module.GetContextForInlineArray(vectorArrayType).ElementType;
					Debug.Assert(SignatureComparer.Default.Equals(vectorArrayType, module.GetTypeSignature(vector2Operand)), "Both vector operands should have the same type");

					TypeSignature maskArrayType = module.GetTypeSignature(maskOperand);
					Debug.Assert(module.GetContextForInlineArray(maskArrayType).ElementType is CorLibTypeSignature { ElementType: ElementType.I4 });

					TypeSignature resultArrayType = module.GetTypeSignature(instruction);
					Debug.Assert(SignatureComparer.Default.Equals(vectorElementType, module.GetContextForInlineArray(resultArrayType).ElementType), "Result array should have the same element type as the vector operands");

					LoadValue(basicBlock, vector1Operand);
					LoadValue(basicBlock, vector2Operand);
					LoadValue(basicBlock, maskOperand);
					IMethodDescriptor method = module.InstructionHelperType.Methods
						.First(m => m.Name == nameof(InstructionHelper.ShuffleVector))
						.MakeGenericInstanceMethod(vectorArrayType, vectorElementType);
					Call(basicBlock, method);
					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMFreeze:
				{
					// At runtime, poison and undefined values don't exist, so freeze is a no-op.
					Debug.Assert(operands.Length == 1, "Freeze instruction should have exactly one operand");
					Debug.Assert(instruction.TypeOf == operands[0].TypeOf, "Freeze instruction should have the same type as its operand");
					LoadValue(basicBlock, operands[0]);
					StoreResult(basicBlock, instruction);
				}
				break;
			case LLVMOpcode.LLVMFence:
				{
					// There's likely no direct equivalent to LLVM fence in CIL.
					// The closest might be Thread.MemoryBarrier and the volatile keyword.
					// https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.memorybarrier
					// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile
					// https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.volatile
					// https://llvm.org/docs/LangRef.html#fence-instruction
					// However, we currently don't do anything except warn.
					Console.WriteLine($"Warning: LLVM fence instruction is not currently supported; it is being ignored inside {function?.Name}.");
				}
				break;
			case LLVMOpcode.LLVMAtomicRMW:
				{
					// These are not currently supported.
					// https://llvm.org/docs/LangRef.html#atomicrmw-instruction
					TypeSignature type = module.GetTypeSignature(instruction);
					LoadVariable(basicBlock, new DefaultVariable(type));
					StoreResult(basicBlock, instruction);
					Console.WriteLine($"Warning: LLVM AtomicRMW instruction is not currently supported; it is being ignored inside {function?.Name}.");
				}
				break;
			case LLVMOpcode.LLVMAtomicCmpXchg:
				{
					// These are not currently supported.
					// https://llvm.org/docs/LangRef.html#cmpxchg-instruction
					TypeSignature type = module.GetTypeSignature(instruction);
					LoadVariable(basicBlock, new DefaultVariable(type));
					StoreResult(basicBlock, instruction);
					Console.WriteLine($"Warning: LLVM CmpXchg instruction is not currently supported; it is being ignored inside {function?.Name}.");
				}
				break;
			default:
				if (BinaryMathInstruction.Supported(opcode))
				{
					Debug.Assert(operands.Length == 2, "Binary math instruction should have exactly two operands");

					// Potential optimization: map "xor v1 0" to "not v1"
					// LLVM doesn't have a not instruction, so it uses xor with zero instead.

					LoadValue(basicBlock, operands[0]);
					LoadValue(basicBlock, operands[1]);

					basicBlock.Add(BinaryMathInstruction.Create(instruction, module));

					StoreResult(basicBlock, instruction);
				}
				else if (NumericalConversionInstruction.Supported(opcode))
				{
					Debug.Assert(operands.Length == 1, "Numerical conversion instruction should have exactly one operand");

					LoadValue(basicBlock, operands[0]);

					basicBlock.Add(NumericalConversionInstruction.Create(instruction, module));

					StoreResult(basicBlock, instruction);
				}
				else
				{
					throw new NotSupportedException($"Unsupported instruction: {opcode}");
				}
				break;
		}
	}

	/// <summary>
	/// The return type of a personality function.
	/// </summary>
	private enum ExceptionDisposition
	{
		/// <summary>
		/// Exception handled; resume execution where it occurred
		/// </summary>
		ContinueExecution,
		/// <summary>
		/// Not handled; search next handler
		/// </summary>
		ContinueSearch,
		/// <summary>
		/// New exception occurred during existing exception handling (unwinding)
		/// </summary>
		NestedException,
		/// <summary>
		/// Unwinding interrupted by another unwind; adjust strategy
		/// </summary>
		/// <remarks>
		/// Can only occur with multi-threading
		/// </remarks>
		CollidedUnwind,
	}

	private void LoadArrayOffset(BasicBlock basicBlock, LLVMValueRef index, TypeSignature elementTypeSignature)
	{
		bool isConstant = index.IsAConstantInt != default;
		long constantValue = index.ConstIntSExt;

		if (isConstant && constantValue == 0)
		{
			// Skip loading zero offset
			return;
		}

		if (elementTypeSignature.TryGetSize(out int size))
		{
			if (isConstant)
			{
				int offset = (int)(constantValue * size);
				LoadVariable(basicBlock, new ConstantI4(offset, module.Definition));
				basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
			}
			else if (size == 1)
			{
				LoadValue(basicBlock, index);
				basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
			}
			else
			{
				LoadValue(basicBlock, index);
				basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
				LoadVariable(basicBlock, new ConstantI4(size, module.Definition));
				basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Mul));
			}
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Add));
		}
		else if (isConstant && constantValue == 1)
		{
			basicBlock.Add(new SizeOfInstruction(elementTypeSignature));
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Add));
		}
		else
		{
			LoadValue(basicBlock, index);
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
			basicBlock.Add(new SizeOfInstruction(elementTypeSignature));
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Mul));
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Add));
		}
	}

	private void LoadArrayOffset(BasicBlock basicBlock, int index, TypeSignature elementTypeSignature)
	{
		if (index == 0)
		{
			// Skip loading zero offset
			return;
		}

		if (elementTypeSignature.TryGetSize(out int size))
		{
			int offset = index * size;
			LoadVariable(basicBlock, new ConstantI4(offset, module.Definition));
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Add));
		}
		else if (index == 1)
		{
			basicBlock.Add(new SizeOfInstruction(elementTypeSignature));
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Add));
		}
		else
		{
			LoadVariable(basicBlock, new ConstantI4(index, module.Definition));
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
			basicBlock.Add(new SizeOfInstruction(elementTypeSignature));
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Mul));
			basicBlock.Add(Instruction.FromOpCode(CilOpCodes.Add));
		}
	}

	private IVariable LoadVariadicArguments(BasicBlock basicBlock, ReadOnlySpan<LLVMValueRef> variadicArguments, ModuleContext module)
	{
		CorLibTypeSignature intPtr = module.Definition.CorLibTypeFactory.IntPtr;

		TypeDefinition intPtrBuffer = module.GetOrCreateInlineArray(intPtr, variadicArguments.Length).Type;

		TypeSignature intPtrSpan = module.Definition.DefaultImporter
			.ImportType(typeof(Span<>))
			.MakeGenericInstanceType(intPtr);

		TypeSignature intPtrReadOnlySpan = module.Definition.DefaultImporter
			.ImportType(typeof(ReadOnlySpan<>))
			.MakeGenericInstanceType(intPtr);

		MethodSignature getItemSignature = MethodSignature.CreateInstance(new GenericParameterSignature(GenericParameterType.Type, 0).MakeByReferenceType(), module.Definition.CorLibTypeFactory.Int32);

		IMethodDescriptor intPtrSpanGetItem = new MemberReference(intPtrSpan.ToTypeDefOrRef(), "get_Item", getItemSignature);

		MethodDefinition inlineArrayAsSpan = module.InlineArrayHelperType.Methods.Single(m => m.Name == nameof(InlineArrayHelper.AsSpan));

		MethodDefinition spanToReadOnly = module.SpanHelperType.Methods.Single(m => m.Name == nameof(SpanHelper.ToReadOnly));

		LocalVariable intPtrBufferLocal = new LocalVariable(intPtrBuffer.ToTypeSignature());
		basicBlock.Add(new InitializeInstruction(intPtrBufferLocal));

		LocalVariable[] variadicLocals = new LocalVariable[variadicArguments.Length];
		for (int i = 0; i < variadicArguments.Length; i++)
		{
			LLVMValueRef variadicArgument = variadicArguments[i];
			LoadValue(basicBlock, variadicArgument);
			LocalVariable local = new LocalVariable(module.GetTypeSignature(variadicArgument));
			basicBlock.Add(new StoreVariableInstruction(local));
			variadicLocals[i] = local;
		}

		LocalVariable intPtrSpanLocal = new LocalVariable(intPtrSpan);
		basicBlock.Add(new AddressOfInstruction(intPtrBufferLocal));
		Call(basicBlock, inlineArrayAsSpan.MakeGenericInstanceMethod(intPtrBuffer.ToTypeSignature(), intPtr));
		basicBlock.Add(new StoreVariableInstruction(intPtrSpanLocal));

		for (int i = 0; i < variadicLocals.Length; i++)
		{
			basicBlock.Add(new AddressOfInstruction(intPtrSpanLocal));
			basicBlock.Add(new LoadVariableInstruction(new ConstantI4(i, module.Definition)));
			basicBlock.Add(new CallInstruction(intPtrSpanGetItem));
			basicBlock.Add(new AddressOfInstruction(variadicLocals[i]));
			basicBlock.Add(new StoreIndirectInstruction(intPtr));
		}

		LocalVariable intPtrReadOnlySpanLocal = new LocalVariable(intPtrReadOnlySpan);
		basicBlock.Add(new LoadVariableInstruction(intPtrSpanLocal));
		basicBlock.Add(new CallInstruction(spanToReadOnly.MakeGenericInstanceMethod(intPtr)));
		basicBlock.Add(new StoreVariableInstruction(intPtrReadOnlySpanLocal));

		return intPtrReadOnlySpanLocal;
	}

	private TypeSignature GetTypeSignature(LLVMValueRef value) => module.GetTypeSignature(value);

	private TypeSignature GetTypeSignature(LLVMTypeRef type) => module.GetTypeSignature(type);

	private unsafe bool TryWriteConstant(LLVMValueRef value, BinaryWriter writer)
	{
		switch (value.Kind)
		{
			case LLVMValueKind.LLVMConstantIntValueKind:
				{
					const int BitsPerByte = 8;
					long integer = value.ConstIntSExt;
					LLVMTypeRef operandType = value.TypeOf;
					switch (operandType.IntWidth)
					{
						case sizeof(sbyte) * BitsPerByte:
							writer.Write((sbyte)integer);
							break;
						case sizeof(short) * BitsPerByte:
							writer.Write((short)integer);
							break;
						case sizeof(int) * BitsPerByte:
							writer.Write((int)integer);
							break;
						case sizeof(long) * BitsPerByte:
							writer.Write(integer);
							break;
						default:
							return false;
					}
				}
				break;
			case LLVMValueKind.LLVMConstantFPValueKind:
				{
					double floatingPoint = value.GetFloatingPointValue();
					switch (value.TypeOf.Kind)
					{
						case LLVMTypeKind.LLVMFloatTypeKind:
							writer.Write((float)floatingPoint);
							break;
						case LLVMTypeKind.LLVMDoubleTypeKind:
							writer.Write(floatingPoint);
							break;
						default:
							return false;
					}
				}
				break;
			case LLVMValueKind.LLVMConstantDataArrayValueKind:
				{
					ReadOnlySpan<byte> data = LibLLVMSharp.ConstantDataArrayGetData(value);
					writer.Write(data);
				}
				break;
			case LLVMValueKind.LLVMConstantArrayValueKind:
				{
					LLVMTargetDataRef targetData = LLVM.GetModuleDataLayout(module.Module);
					int size = (int)targetData.ABISizeOfType(value.TypeOf);

					writer.Flush();
					long arrayStartPosition = writer.BaseStream.Position;

					LLVMValueRef[] elements = value.GetOperands();
					foreach (LLVMValueRef element in elements)
					{
						if (!TryWriteConstant(element, writer))
						{
							return false;
						}
					}

					writer.Flush();
					long arrayEndPosition = writer.BaseStream.Position;

					Debug.Assert(arrayEndPosition - arrayStartPosition == size, "Array size should match the expected size");
				}
				break;
			case LLVMValueKind.LLVMConstantStructValueKind:
				{
					LLVMTargetDataRef targetData = LLVM.GetModuleDataLayout(module.Module);
					ulong size = targetData.ABISizeOfType(value.TypeOf);

					// Write zeroes in case of padding
					writer.Flush();
					long structStartPosition = writer.BaseStream.Position;
					WriteZeroes(writer, (int)size);
					writer.Flush();
					long structEndPosition = writer.BaseStream.Position;
					writer.BaseStream.Position = structStartPosition;

					// Write each field
					LLVMValueRef[] fields = value.GetOperands();
					for (int i = 0; i < fields.Length; i++)
					{
						LLVMValueRef field = fields[i];
						ulong fieldOffset = targetData.OffsetOfElement(value.TypeOf, (uint)i);
						writer.BaseStream.Position = structStartPosition + (long)fieldOffset;
						if (!TryWriteConstant(field, writer))
						{
							return false;
						}
					}

					// Navigate to the end of the struct
					writer.Flush();
					writer.BaseStream.Position = structEndPosition;
				}
				break;
			case LLVMValueKind.LLVMConstantPointerNullValueKind:
			case LLVMValueKind.LLVMConstantAggregateZeroValueKind:
			case LLVMValueKind.LLVMUndefValueValueKind:
				{
					// Write zeroes
					LLVMTargetDataRef targetData = LLVM.GetModuleDataLayout(module.Module);
					ulong size = targetData.ABISizeOfType(value.TypeOf);
					WriteZeroes(writer, (int)size);
				}
				break;
			default:
				return false;
		}
		return true;

		static void WriteZeroes(BinaryWriter writer, int count)
		{
			const int StackAllocSize = 256;
			Span<byte> buffer = stackalloc byte[StackAllocSize];
			buffer.Clear();
			while (count > 0)
			{
				int writeCount = int.Min(count, StackAllocSize);
				writer.Write(buffer[..writeCount]);
				count -= writeCount;
			}
		}
	}

	private bool TryWriteConstant(LLVMValueRef value, [NotNullWhen(true)] out byte[]? data)
	{
		using MemoryStream memoryStream = new();
		using BinaryWriter writer = new(memoryStream);
		if (TryWriteConstant(value, writer))
		{
			writer.Flush();
			data = memoryStream.ToArray();
			return true;
		}
		else
		{
			data = null;
			return false;
		}
	}

	private void LoadValue(BasicBlock basicBlock, LLVMValueRef value)
	{
		switch (value.Kind)
		{
			case LLVMValueKind.LLVMConstantIntValueKind:
				{
					const int BitsPerByte = 8;
					long integer = value.ConstIntSExt;
					LLVMTypeRef operandType = value.TypeOf;
					TypeSignature typeSignature = module.GetTypeSignature(operandType);
					if (integer is <= int.MaxValue and >= int.MinValue && operandType is { IntWidth: <= sizeof(int) * BitsPerByte })
					{
						LoadVariable(basicBlock, new ConstantI4((int)integer, module.Definition));
					}
					else if (operandType is { IntWidth: sizeof(long) * BitsPerByte })
					{
						LoadVariable(basicBlock, new ConstantI8(integer, module.Definition));
					}
					else if (operandType is { IntWidth: 2 * sizeof(long) * BitsPerByte })
					{
						LoadVariable(basicBlock, new ConstantI8(integer, module.Definition));
						MethodDefinition conversionMethod = typeSignature.Resolve()!.Methods.First(m =>
						{
							return m.Name == "op_Implicit" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType is CorLibTypeSignature { ElementType: ElementType.I8 };
						});
						Call(basicBlock, module.Definition.DefaultImporter.ImportMethod(conversionMethod));
					}
					else
					{
						throw new NotSupportedException($"Unsupported integer type: {typeSignature}");
					}
				}
				break;
			case LLVMValueKind.LLVMGlobalVariableValueKind:
				{
					GlobalVariableContext global = module.GlobalVariables[value];
					basicBlock.Add(new AddressOfInstruction(global));
				}
				break;
			case LLVMValueKind.LLVMGlobalAliasValueKind:
				{
					LLVMValueRef[] operands = value.GetOperands();
					if (operands.Length == 1)
					{
						LoadValue(basicBlock, operands[0]);
					}
					else
					{
						throw new NotSupportedException();
					}
				}
				break;
			case LLVMValueKind.LLVMConstantFPValueKind:
				{
					double floatingPoint = value.GetFloatingPointValue();
					TypeSignature typeSignature = GetTypeSignature(value.TypeOf);
					switch (typeSignature)
					{
						case CorLibTypeSignature { ElementType: ElementType.R4 }:
							LoadVariable(basicBlock, new ConstantR4((float)floatingPoint, module.Definition));
							break;
						case CorLibTypeSignature { ElementType: ElementType.R8 }:
							LoadVariable(basicBlock, new ConstantR8(floatingPoint, module.Definition));
							break;
						default:
							throw new NotSupportedException();
					}
				}
				break;
			case LLVMValueKind.LLVMConstantDataArrayValueKind:
				{
					ReadOnlySpan<byte> data = LibLLVMSharp.ConstantDataArrayGetData(value);

					TypeSignature arrayType = GetTypeSignature(value.TypeOf);

					module.InlineArrayTypes[(TypeDefinition)arrayType.ToTypeDefOrRef()].GetUltimateElementType(out TypeSignature elementType, out int elementCount);

					LoadArrayFromByteSpan(basicBlock, arrayType, elementType, data);
				}
				break;
			case LLVMValueKind.LLVMConstantArrayValueKind:
				{
					TypeSignature underlyingType = GetTypeSignature(value.TypeOf);

					module.InlineArrayTypes[(TypeDefinition)underlyingType.ToTypeDefOrRef()].GetElementType(out TypeSignature elementType, out int elementCount);

					if (module.Options.PrecomputeInitializers && TryWriteConstant(value, out byte[]? data))
					{
						LoadArrayFromByteSpan(basicBlock, underlyingType, elementType, data);
						return;
					}

					LLVMValueRef[] elements = value.GetOperands();

					if (elementCount != elements.Length)
					{
						throw new Exception("Array element count mismatch");
					}

					Debug.Assert(elementType is not PointerTypeSignature, "Pointers cannot be used as generic type arguments");

					TypeSignature spanType = module.Definition.DefaultImporter
						.ImportType(typeof(Span<>))
						.MakeGenericInstanceType(elementType);

					IMethodDescriptor inlineArrayAsSpan = module.InlineArrayHelperType.Methods
						.Single(m => m.Name == nameof(InlineArrayHelper.AsSpan))
						.MakeGenericInstanceMethod(underlyingType, elementType);

					MethodSignature getItemSignature = MethodSignature.CreateInstance(new GenericParameterSignature(GenericParameterType.Type, 0).MakeByReferenceType(), module.Definition.CorLibTypeFactory.Int32);
					IMethodDescriptor getItem = new MemberReference(spanType.ToTypeDefOrRef(), "get_Item", getItemSignature);

					LocalVariable bufferLocal = new(underlyingType);
					LocalVariable spanLocal = new(spanType);

					basicBlock.Add(new InitializeInstruction(spanLocal));

					basicBlock.Add(new AddressOfInstruction(bufferLocal));
					basicBlock.Add(new CallInstruction(inlineArrayAsSpan));
					basicBlock.Add(new StoreVariableInstruction(spanLocal));

					for (int i = 0; i < elements.Length; i++)
					{
						LLVMValueRef element = elements[i];
						basicBlock.Add(new AddressOfInstruction(spanLocal));
						LoadVariable(basicBlock, new ConstantI4(i, module.Definition));
						Call(basicBlock, getItem);
						LoadValue(basicBlock, element);
						basicBlock.Add(new StoreIndirectInstruction(elementType));
					}

					basicBlock.Add(new LoadVariableInstruction(bufferLocal));
				}
				break;
			case LLVMValueKind.LLVMConstantStructValueKind:
				{
					TypeSignature typeSignature = GetTypeSignature(value.TypeOf);

					if (module.Options.PrecomputeInitializers && TryWriteConstant(value, out byte[]? data))
					{
						IMethodDefOrRef spanConstructor = (IMethodDefOrRef)module.Definition.DefaultImporter
							.ImportMethod(typeof(ReadOnlySpan<byte>).GetConstructor([typeof(void*), typeof(int)])!);

						FieldDefinition field = module.AddStoredDataField(data);

						basicBlock.Add(new LoadFieldAddressInstruction(field));
						basicBlock.Add(new LoadVariableInstruction(new ConstantI4(data.Length, module.Definition)));
						basicBlock.Add(new NewObjectInstruction(spanConstructor));

						IMethodDefOrRef read = (IMethodDefOrRef)module.Definition.DefaultImporter
							.ImportMethod(typeof(MemoryMarshal).GetMethod(nameof(MemoryMarshal.Read))!);
						IMethodDescriptor readInstance = read.MakeGenericInstanceMethod(typeSignature);

						Call(basicBlock, readInstance);

						return;
					}

					TypeDefinition typeDefinition = (TypeDefinition)typeSignature.ToTypeDefOrRef();

					LLVMValueRef[] fields = value.GetOperands();
					if (fields.Length != typeDefinition.Fields.Count)
					{
						throw new Exception("Struct field count mismatch");
					}

					LocalVariable resultLocal = new(typeSignature);

					basicBlock.Add(new InitializeInstruction(resultLocal));

					for (int i = 0; i < fields.Length; i++)
					{
						LLVMValueRef field = fields[i];
						FieldDefinition fieldDefinition = typeDefinition.Fields[i];

						basicBlock.Add(new AddressOfInstruction(resultLocal));
						basicBlock.Add(new LoadFieldAddressInstruction(fieldDefinition));
						LoadValue(basicBlock, field);
						basicBlock.Add(new StoreIndirectInstruction(fieldDefinition.Signature!.FieldType));
					}

					LoadVariable(basicBlock, resultLocal);
				}
				break;
			case LLVMValueKind.LLVMConstantPointerNullValueKind:
			case LLVMValueKind.LLVMConstantAggregateZeroValueKind:
			case LLVMValueKind.LLVMUndefValueValueKind:
				{
					TypeSignature typeSignature = GetTypeSignature(value.TypeOf);
					LoadVariable(basicBlock, new DefaultVariable(typeSignature));
				}
				break;
			case LLVMValueKind.LLVMFunctionValueKind:
				{
					LoadVariable(basicBlock, new FunctionPointerVariable(module.Methods[value]));
				}
				break;
			case LLVMValueKind.LLVMConstantExprValueKind:
				{
					TypeSignature resultType = module.GetTypeSignature(value);
					if (resultType is not CorLibTypeSignature { ElementType: ElementType.Void })
					{
						LocalVariable resultVariable = new(resultType);
						instructionResults.Add(value, resultVariable);
						AddInstruction(basicBlock, value);
						LoadVariable(basicBlock, resultVariable);
						instructionResults.Remove(value);
					}
					else
					{
						throw new InvalidOperationException("Constant expressions should not be void.");
					}
				}
				break;
			case LLVMValueKind.LLVMInstructionValueKind:
				{
					if (value.InstructionOpcode is LLVMOpcode.LLVMAlloca)
					{
						basicBlock.Add(new AddressOfInstruction(allocaData[value]));
					}
					else
					{
						LoadVariable(basicBlock, instructionResults[value]);
					}
				}
				break;
			case LLVMValueKind.LLVMArgumentValueKind:
				{
					LoadVariable(basicBlock, module.Methods[value.ParamParent].ParameterLookup[value]);
				}
				break;
			case LLVMValueKind.LLVMMetadataAsValueValueKind:
				{
					//Metadata is not a real type, so we just use Object. Anywhere metadata is supposed to be loaded, we instead load a null value.
					TypeSignature typeSignature = module.Definition.CorLibTypeFactory.Object;
					LoadVariable(basicBlock, new DefaultVariable(typeSignature));
				}
				break;
			default:
				throw new NotImplementedException(value.Kind.ToString());
		}
	}

	private void LoadArrayFromByteSpan(BasicBlock basicBlock, TypeSignature arrayType, TypeSignature elementType, ReadOnlySpan<byte> data)
	{
		if (elementType is CorLibTypeSignature { ElementType: ElementType.I2 } && data.TryParseCharacterArray(out string? @string))
		{
			elementType = module.Definition.CorLibTypeFactory.Char;

			IMethodDescriptor toCharacterSpan = module.SpanHelperType.Methods
				.Single(m => m.Name == nameof(SpanHelper.ToCharacterSpan));

			LoadVariable(basicBlock, new ConstantString(@string, module.Definition));
			Call(basicBlock, toCharacterSpan);
		}
		else if (elementType is CorLibTypeSignature { ElementType: ElementType.I1 or ElementType.U1 })
		{
			elementType = module.Definition.CorLibTypeFactory.Byte;

			IMethodDefOrRef spanConstructor = (IMethodDefOrRef)module.Definition.DefaultImporter
				.ImportMethod(typeof(ReadOnlySpan<byte>).GetConstructor([typeof(void*), typeof(int)])!);

			FieldDefinition field = module.AddStoredDataField(data);

			basicBlock.Add(new LoadFieldAddressInstruction(field));
			basicBlock.Add(new LoadVariableInstruction(new ConstantI4(data.Length, module.Definition)));
			basicBlock.Add(new NewObjectInstruction(spanConstructor));
		}
		else if (elementType is CorLibTypeSignature)
		{
			IMethodDefOrRef createSpan = (IMethodDefOrRef)module.Definition.DefaultImporter
				.ImportMethod(typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateSpan))!);
			IMethodDescriptor createSpanInstance = createSpan.MakeGenericInstanceMethod(elementType);

			FieldDefinition field = module.AddStoredDataField(data);

			basicBlock.Add(new LoadTokenInstruction(field));
			basicBlock.Add(new CallInstruction(createSpanInstance));
		}
		else
		{
			IMethodDefOrRef spanConstructor = (IMethodDefOrRef)module.Definition.DefaultImporter
				.ImportMethod(typeof(ReadOnlySpan<byte>).GetConstructor([typeof(void*), typeof(int)])!);

			FieldDefinition field = module.AddStoredDataField(data);

			basicBlock.Add(new LoadFieldAddressInstruction(field));
			basicBlock.Add(new LoadVariableInstruction(new ConstantI4(data.Length, module.Definition)));
			basicBlock.Add(new NewObjectInstruction(spanConstructor));

			IMethodDescriptor castMethod = module.SpanHelperType.Methods
				.Single(m => m.Name == nameof(SpanHelper.Cast))
				.MakeGenericInstanceMethod(module.Definition.CorLibTypeFactory.Byte, elementType);

			Call(basicBlock, castMethod);
		}

		Debug.Assert(elementType is not PointerTypeSignature, "Pointers cannot be used as generic type arguments");

		IMethodDescriptor createInlineArray = module.InlineArrayHelperType.Methods
			.Single(m => m.Name == nameof(InlineArrayHelper.Create))
			.MakeGenericInstanceMethod(arrayType, elementType);

		Call(basicBlock, createInlineArray);
	}

	private void StoreResult(BasicBlock basicBlock, LLVMValueRef instruction)
	{
		basicBlock.Add(new StoreVariableInstruction(instructionResults[instruction]));
	}

	private void MaybeStoreResult(BasicBlock basicBlock, LLVMValueRef instruction)
	{
		if (instructionResults.TryGetValue(instruction, out IVariable? result))
		{
			basicBlock.Add(new StoreVariableInstruction(result));
		}
	}

	private static void LoadVariable(BasicBlock basicBlock, IVariable variable)
	{
		basicBlock.Add(new LoadVariableInstruction(variable));
	}

	private static void Call(BasicBlock instructions, IMethodDescriptor method)
	{
		instructions.Add(new CallInstruction(method));
	}

	/// <summary>
	/// Includes handling for phi instructions in the target block.
	/// </summary>
	private void Branch(BasicBlock basicBlock, LLVMBasicBlockRef target)
	{
		Branch(basicBlock, basicBlockRefs[basicBlock], target);
	}

	/// <summary>
	/// Includes handling for phi instructions in the target block.
	/// </summary>
	private void Branch(BasicBlock basicBlock, LLVMBasicBlockRef source, LLVMBasicBlockRef target)
	{
		foreach (LLVMValueRef targetInstruction in target.GetInstructions())
		{
			if (targetInstruction.IsAPHINode == default)
			{
				break;
			}

			LLVMValueRef value = targetInstruction.GetOperandForIncomingBlock(source);
			LoadValue(basicBlock, value);
			basicBlock.Add(PhiPopInstruction.Instance);
		}
		basicBlock.Add(new UnconditionalBranchInstruction(basicBlocks[target]));
	}

	/// <summary>
	/// Includes handling for phi instructions in the target blocks.
	/// </summary>
	private void ConditionalBranch(BasicBlock basicBlock, LLVMBasicBlockRef trueBlock, LLVMBasicBlockRef falseBlock)
	{
		if (!trueBlock.StartsWithPhi())
		{
			// The true block does not start with a phi instruction.
			// The false block might or might not start with a phi instruction.

			basicBlock.Add(new BranchIfTrueInstruction(basicBlocks[trueBlock]));

			Branch(basicBlock, falseBlock);
		}
		else if (!falseBlock.StartsWithPhi())
		{
			// Only the true block starts with a phi instruction.

			basicBlock.Add(new BranchIfFalseInstruction(basicBlocks[falseBlock]));

			Branch(basicBlock, trueBlock);
		}
		else
		{
			// Both target blocks start with a phi instruction.
			// We arbitrarily create a helper basic block to handle the true case.

			BasicBlock helperBasicBlock = new();
			basicBlockList.Add(helperBasicBlock);

			basicBlock.Add(new BranchIfTrueInstruction(helperBasicBlock));

			Branch(basicBlock, falseBlock);

			Branch(helperBasicBlock, basicBlockRefs[basicBlock], trueBlock);
		}
	}

	private static bool TryMatchImageOffset(LLVMValueRef instruction, ModuleContext module, out FunctionContext? function, out GlobalVariableContext? variable)
	{
		if (instruction.Kind is not LLVMValueKind.LLVMConstantExprValueKind)
		{
			return False(out function, out variable);
		}

		LLVMValueRef trunc = instruction;
		if (trunc.ConstOpcode is not LLVMOpcode.LLVMTrunc || trunc.TypeOf is not { Kind: LLVMTypeKind.LLVMIntegerTypeKind, IntWidth: 32 })
		{
			return False(out function, out variable);
		}

		LLVMValueRef sub = trunc.GetOperand(0);
		if (sub.ConstOpcode is not LLVMOpcode.LLVMSub || sub.TypeOf is not { Kind: LLVMTypeKind.LLVMIntegerTypeKind, IntWidth: 64 })
		{
			return False(out function, out variable);
		}

		LLVMValueRef ptrToInt_Left = sub.GetOperand(0);
		LLVMValueRef ptrToInt_Right = sub.GetOperand(1);
		if (ptrToInt_Left.ConstOpcode is not LLVMOpcode.LLVMPtrToInt || ptrToInt_Right.ConstOpcode is not LLVMOpcode.LLVMPtrToInt)
		{
			return False(out function, out variable);
		}

		LLVMValueRef imageBase = ptrToInt_Right.GetOperand(0);
		if (imageBase.Kind is not LLVMValueKind.LLVMGlobalVariableValueKind || imageBase.Name is not "__ImageBase")
		{
			return False(out function, out variable);
		}

		LLVMValueRef address = ptrToInt_Left.GetOperand(0);
		if (address.Kind is LLVMValueKind.LLVMFunctionValueKind)
		{
			function = module.Methods[address];
			variable = null;
			return true;
		}
		else if (address.Kind is LLVMValueKind.LLVMGlobalVariableValueKind)
		{
			variable = module.GlobalVariables[address];
			function = null;
			return true;
		}
		else
		{
			return False(out function, out variable);
		}

		static bool False(out FunctionContext? function, out GlobalVariableContext? variable)
		{
			function = null;
			variable = null;
			return false;
		}
	}
}
