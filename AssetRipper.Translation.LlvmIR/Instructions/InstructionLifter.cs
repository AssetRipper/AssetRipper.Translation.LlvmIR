using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Variables;
using LLVMSharp.Interop;
using System.Diagnostics;
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
	private readonly Dictionary<LLVMValueRef, LocalVariable> instructionResults = new();
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

	private void AddInstruction(BasicBlock instructions, LLVMValueRef instruction)
	{
		if (TryMatchImageOffset(instruction, module, out FunctionContext? function2, out GlobalVariableContext? variable2))
		{
			MethodDefinition getIndexMethod = module.InjectedTypes[typeof(PointerIndices)].GetMethodByName(nameof(PointerIndices.GetIndex));

			if (function2 is not null)
			{
				LoadVariable(instructions, new FunctionPointerVariable(function2));
			}
			else if (variable2 is not null)
			{
				instructions.Add(new AddressOfInstruction(variable2));
			}
			else
			{
				Debug.Fail("This should be unreachable.");
			}
			Call(instructions, getIndexMethod);
			StoreResult(instructions, instruction);
			return;
		}

		LLVMValueRef[] operands = instruction.GetOperands();
		LLVMOpcode opcode = instruction.GetOpcode();
		switch (opcode)
		{
			case LLVMOpcode.LLVMAlloca:
				{
					TypeSignature allocatedType = module.GetTypeSignature(LLVM.GetAllocatedType(instruction));
					LLVMValueRef sizeOperand = operands[0];
					long fixedSize = sizeOperand.ConstIntSExt;
					TypeSignature dataType = fixedSize != 1
						? module.GetOrCreateInlineArray(allocatedType, (int)fixedSize).Type.ToTypeSignature()
						: allocatedType;

					IVariable dataLocal = function is not null && function.MightThrowAnException ? new FunctionFieldVariable(dataType, function) : new LocalVariable(dataType);
					instructions.Add(new InitializeInstruction(dataLocal));
					instructions.Add(new AddressOfInstruction(dataLocal));
					instructions.Add(new StoreVariableInstruction(instructionResults[instruction]));
				}
				break;
			case LLVMOpcode.LLVMLoad:
				{
					Debug.Assert(operands.Length == 1, "Load instruction should have exactly one operand");

					LLVMValueRef sourceOperand = operands[0];
					LoadValue(instructions, sourceOperand);

					TypeSignature type = module.GetTypeSignature(instruction);
					instructions.Add(new LoadIndirectInstruction(type));

					StoreResult(instructions, instruction);
				}
				break;
			case LLVMOpcode.LLVMStore:
				{
					Debug.Assert(operands.Length == 2, "Store instruction should have exactly two operands");

					LLVMValueRef valueOperand = operands[0];
					LLVMValueRef pointerOperand = operands[1];

					LoadValue(instructions, pointerOperand);
					LoadValue(instructions, valueOperand);

					TypeSignature type = module.GetTypeSignature(valueOperand);
					instructions.Add(new StoreIndirectInstruction(type));
				}
				break;
			case LLVMOpcode.LLVMRet:
				{
					Debug.Assert(operands.Length <= 1, "Return instruction should have at most one operand");
					if (operands.Length is 1)
					{
						LoadValue(instructions, operands[0]);
					}
					if (function is { MightThrowAnException: true })
					{
						instructions.Add(new ClearStackFrameInstruction(function));
					}
					instructions.Add(operands.Length == 0 ? ReturnInstruction.Void : ReturnInstruction.Value);
				}
				break;
			case LLVMOpcode.LLVMBr:
				{
					Debug.Assert(instructions is BasicBlock);
					if (operands.Length == 1)
					{
						LLVMBasicBlockRef targetBlockRef = operands[0].AsBasicBlock();

						Branch(instructions, targetBlockRef);
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

						LoadValue(instructions, condition);

						ConditionalBranch(instructions, trueBlock, falseBlock);
					}
					else
					{
						throw new NotSupportedException($"Unsupported branch instruction with {operands.Length} operands");
					}
				}
				break;
			case LLVMOpcode.LLVMSwitch:
				{
					Debug.Assert(instructions is BasicBlock);
					Debug.Assert(operands.Length >= 2);
					Debug.Assert(operands.Length % 2 == 0);

					LLVMValueRef indexOperand = operands[0];
					LLVMBasicBlockRef defaultBlockRef = operands[1].AsBasicBlock();
					ReadOnlySpan<(LLVMValueRef Case, LLVMValueRef Target)> cases = MemoryMarshal.Cast<LLVMValueRef, (LLVMValueRef Case, LLVMValueRef Target)>(operands.AsSpan(2));

					LoadValue(instructions, indexOperand);

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

							Branch(helperBasicBlock, basicBlockRefs[instructions], targetBlockRef);

							targetBlock = helperBasicBlock;
						}
						else
						{
							targetBlock = basicBlocks[targetBlockRef];
						}
						caseTargets[i] = (caseValue.ConstIntSExt, basicBlocks[targetBlockRef]);
					}

					TypeSignature indexType = module.GetTypeSignature(indexOperand);
					instructions.Add(new SwitchInstruction(indexType, caseTargets));

					Branch(instructions, defaultBlockRef);
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

					LoadValue(instructions, condition);

					LoadValue(instructions, trueValue);

					LoadValue(instructions, falseValue);

					if (module.GetTypeSignature(condition) is CorLibTypeSignature { ElementType: ElementType.Boolean })
					{
						TypeSignature valueTypeSignature = module.GetTypeSignature(trueValue);

						if (valueTypeSignature is PointerTypeSignature)
						{
							throw new NotImplementedException();
						}

						IMethodDescriptor helperMethod = module.InstructionHelperType.Methods
							.First(m => m.Name == nameof(InstructionHelper.Select) && m.GenericParameters.Count is 1)
							.MakeGenericInstanceMethod(valueTypeSignature);

						Call(instructions, helperMethod);
					}
					else
					{
						throw new NotImplementedException("Non-boolean condition for select instructions");
					}

					StoreResult(instructions, instruction);
				}
				break;
			case LLVMOpcode.LLVMPHI:
				{
					Debug.Assert(operands.Length > 0, "Phi instruction should have at least one operand");

					// Create a phi instruction with all incoming values
					/*(IReadOnlyList<Instruction> value, BasicBlock source)[] sources = new (IReadOnlyList<Instruction>, BasicBlock)[operands.Length];

					for (uint i = 0; i < operands.Length; i++)
					{
						LLVMValueRef operand = operands[i];
						List<Instruction> valueInstructions = new();
						LoadValue(valueInstructions, operand);
						sources[i] = (valueInstructions, basicBlocks[instruction.GetIncomingBlock(i)]);
					}
					instructions.Add(new PhiInstruction(sources));*/

					instructions.Add(PhiPushInstruction.Instance);

					StoreResult(instructions, instruction);
				}
				break;
			case LLVMOpcode.LLVMUnreachable:
				{
					instructions.Add(UnreachableInstruction.Instance);
				}
				break;
			case LLVMOpcode.LLVMICmp:
				{
					Debug.Assert(operands.Length == 2);

					LoadValue(instructions, operands[0]);
					LoadValue(instructions, operands[1]);

					TypeSignature type = module.GetTypeSignature(operands[0]);
					instructions.Add(NumericalComparisonInstruction.Create(type, instruction.ICmpPredicate));

					StoreResult(instructions, instruction);
				}
				break;
			case LLVMOpcode.LLVMFCmp:
				{
					Debug.Assert(operands.Length == 2);

					LoadValue(instructions, operands[0]);
					LoadValue(instructions, operands[1]);

					TypeSignature type = module.GetTypeSignature(operands[0]);
					instructions.Add(NumericalComparisonInstruction.Create(type, instruction.FCmpPredicate));

					StoreResult(instructions, instruction);
				}
				break;
			case LLVMOpcode.LLVMFNeg:
				{
					Debug.Assert(operands.Length == 1, "Unary negation instruction should have exactly one operand");

					LoadValue(instructions, operands[0]);

					TypeSignature type = module.GetTypeSignature(instruction);
					instructions.Add(NegationInstruction.Create(type, module));

					StoreResult(instructions, instruction);
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

					LoadValue(instructions, operands[0]);
					Call(instructions, method);
				}
				break;
			case LLVMOpcode.LLVMVAArg:
				{
					// On Windows, this doesn't get used because Clang optimizes va_list away.

					Debug.Assert(operands.Length == 1);

					LoadValue(instructions, operands[0]);

					Call(instructions, module.InstructionHelperType.Methods.First(m => m.Name == nameof(InstructionHelper.VAArg)));

					TypeSignature resultType = module.GetTypeSignature(instruction);
					instructions.Add(new LoadIndirectInstruction(resultType));

					StoreResult(instructions, instruction);
				}
				break;
			case LLVMOpcode.LLVMCatchSwitch:
				{
					Debug.Assert(operands.Length >= 1, "Catch switch instruction should have at least one operand");

					Debug.Assert(instructions is BasicBlock);

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

						IVariable argumentsInReadOnlySpan = LoadVariadicArguments(instructions, catchPadArguments, module);

						// Call personality function
						instructions.Add(new LoadVariableInstruction(argumentsInReadOnlySpan));
						Call(instructions, function.PersonalityFunction.Definition);

						BasicBlock targetBlock;
						if (catchPadBasicBlockRef.StartsWithPhi())
						{
							BasicBlock helperBasicBlock = new();
							basicBlockList.Add(helperBasicBlock);

							Branch(helperBasicBlock, basicBlockRefs[instructions], catchPadBasicBlockRef);

							targetBlock = helperBasicBlock;
						}
						else
						{
							targetBlock = basicBlocks[catchPadBasicBlockRef];
						}

						instructions.Add(new BranchIfFalseInstruction(targetBlock));
					}

					if (hasDefaultUnwind)
					{
						Branch(instructions, defaultUnwindTargetRef);
					}
					else
					{
						// Unwind to caller
						instructions.Add(new ReturnDefaultInstruction(function.Definition.Signature!.ReturnType));
					}
				}
				break;
			case LLVMOpcode.LLVMCatchPad:
			case LLVMOpcode.LLVMCleanupPad:
				{
					FieldDefinition exceptionInfoField = module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current));

					// Store the current exception info in a local variable
					instructions.Add(new LoadFieldInstruction(exceptionInfoField));
					StoreResult(instructions, instruction);

					// Set the current exception info to null
					LoadVariable(instructions, new DefaultVariable(exceptionInfoField.Signature!.FieldType));
					instructions.Add(new StoreFieldInstruction(exceptionInfoField));
				}
				break;
			case LLVMOpcode.LLVMCatchRet:
				{
					Debug.Assert(instructions is BasicBlock);
					Debug.Assert(operands.Length == 2, "Catch return instruction should have exactly two operands");
					Debug.Assert(operands[0].IsACatchPadInst != default, "First operand of catch return instruction should be a catch pad");
					Debug.Assert(operands[1].IsBasicBlock, "Second operand of catch return instruction should be a basic block");

					LLVMValueRef catchPad = operands[0];
					LLVMBasicBlockRef targetBlockRef = operands[1].AsBasicBlock();

					LoadValue(instructions, catchPad);

					Call(instructions, module.InjectedTypes[typeof(ExceptionInfo)].Methods.Single(m => m.Name == nameof(ExceptionInfo.Dispose) && m.IsPublic));

					Branch(instructions, targetBlockRef);
				}
				break;
			case LLVMOpcode.LLVMCleanupRet:
				{
					Debug.Assert(function is not null);
					Debug.Assert(instructions is BasicBlock);
					Debug.Assert(operands.Length is 1 or 2, "Cleanup return instruction should have one or two operands");

					LLVMValueRef cleanupPad = operands[0];
					bool unwindsToCaller = operands.Length == 1;

					FieldDefinition exceptionInfoField = module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current));

					// Restore the current exception info from the cleanup pad
					LoadValue(instructions, cleanupPad);
					instructions.Add(new StoreFieldInstruction(exceptionInfoField));

					if (unwindsToCaller)
					{
						instructions.Add(new ReturnDefaultInstruction(function.Definition.Signature!.ReturnType));
					}
					else
					{
						// Unwind to an exception handler switch or another cleanup pad
						Branch(instructions, operands[1].AsBasicBlock());
					}
				}
				break;
			case LLVMOpcode.LLVMCall:
			case LLVMOpcode.LLVMInvoke:
				{
					Debug.Assert(instructions is BasicBlock);
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
							LoadValue(instructions, argumentOperand);
						}

						LoadValue(instructions, functionOperand);

						TypeSignature[] parameterTypes = new TypeSignature[argumentOperands.Length];
						for (int i = 0; i < argumentOperands.Length; i++)
						{
							LLVMValueRef operand = argumentOperands[i];
							parameterTypes[i] = module.GetTypeSignature(operand);
						}
						TypeSignature resultTypeSignature = module.GetTypeSignature(instruction);
						MethodSignature methodSignature = MethodSignature.CreateStatic(resultTypeSignature, parameterTypes);

						instructions.Add(new CallIndirectInstruction(methodSignature));
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

						LoadValue(instructions, argumentOperands[0]);

						instructions.Add(new LoadVariableInstruction(function.VariadicParameter));

						Call(instructions, module.InstructionHelperType.GetMethodByName(nameof(InstructionHelper.VAStart)));
					}
					else
					{
						int variadicParameterCount = argumentOperands.Length - functionCalled.NormalParameters.Length;

						if (!functionCalled.IsVariadic)
						{
							Debug.Assert(variadicParameterCount == 0, "Function should not have variadic parameters");

							foreach (LLVMValueRef argumentOperand in argumentOperands)
							{
								LoadValue(instructions, argumentOperand);
							}
						}
						else if (variadicParameterCount == 0)
						{
							for (int i = 0; i < functionCalled.NormalParameters.Length; i++)
							{
								LoadValue(instructions, argumentOperands[i]);
							}
							TypeSignature variadicArrayType = functionCalled.Definition.Signature!.ParameterTypes[^1];
							instructions.Add(new LoadVariableInstruction(new DefaultVariable(variadicArrayType)));
						}
						else
						{
							IVariable intPtrReadOnlySpanLocal = LoadVariadicArguments(instructions, argumentOperands[functionCalled.NormalParameters.Length..], module);

							// Push the arguments onto the stack
							for (int i = 0; i < functionCalled.NormalParameters.Length; i++)
							{
								LoadValue(instructions, argumentOperands[i]);
							}
							instructions.Add(new LoadVariableInstruction(intPtrReadOnlySpanLocal));
						}

						Call(instructions, functionCalled.Definition);
					}

					MaybeStoreResult(instructions, instruction);

					if (opcode is LLVMOpcode.LLVMCall)
					{
						if (functionCalled is null or { MightThrowAnException: true } && function.MightThrowAnException)
						{
							instructions.Add(ReturnIfExceptionInfoNotNullInstruction.Create(function.Definition.Signature!.ReturnType, module));
						}
					}
					else if (opcode is LLVMOpcode.LLVMInvoke)
					{
						LLVMBasicBlockRef catchBlockRef = operands[^2].AsBasicBlock();
						LLVMBasicBlockRef defaultBlockRef = operands[^3].AsBasicBlock();

						instructions.Add(new LoadFieldInstruction(module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current))));
						ConditionalBranch(instructions, catchBlockRef, defaultBlockRef);
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

					LoadValue(instructions, source);
					LoadArrayOffset(instructions, initialIndex, sourceElementTypeSignature);

					TypeSignature currentType = sourceElementTypeSignature;
					foreach (LLVMValueRef operand in otherIndices)
					{
						LLVMTypeRef operandType = operand.TypeOf;

						operandType.ThrowIfNotCoreLibInteger();

						TypeDefOrRefSignature structTypeSignature = (TypeDefOrRefSignature)currentType;
						TypeDefinition structType = (TypeDefinition)structTypeSignature.ToTypeDefOrRef();

						if (module.InlineArrayTypes.TryGetValue(structType, out InlineArrayContext? inlineArray))
						{
							LoadArrayOffset(instructions, operand, inlineArray.ElementType);
							currentType = inlineArray.ElementType;
						}
						else
						{
							Debug.Assert(operand.Kind == LLVMValueKind.LLVMConstantIntValueKind);

							int index = (int)operand.ConstIntSExt;
							FieldDefinition field = structType.GetInstanceField(index);
							instructions.Add(new LoadFieldAddressInstruction(field));
							currentType = field.Signature!.FieldType;
						}
					}

					StoreResult(instructions, instruction);
				}
				break;
			default:
				if (BinaryMathInstruction.Supported(opcode))
				{
					Debug.Assert(operands.Length == 2, "Binary math instruction should have exactly two operands");

					// Potential optimization: map "xor v1 0" to "not v1"
					// LLVM doesn't have a not instruction, so it uses xor with zero instead.

					LoadValue(instructions, operands[0]);
					LoadValue(instructions, operands[1]);

					instructions.Add(BinaryMathInstruction.Create(instruction, module));

					StoreResult(instructions, instruction);
				}
				else if (NumericalConversionInstruction.Supported(opcode))
				{
					Debug.Assert(operands.Length == 1, "Numerical conversion instruction should have exactly one operand");

					LoadValue(instructions, operands[0]);

					instructions.Add(NumericalConversionInstruction.Create(instruction, module));

					StoreResult(instructions, instruction);
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

	private void LoadArrayOffset(BasicBlock instructions, LLVMValueRef index, TypeSignature elementTypeSignature)
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
				LoadVariable(instructions, new ConstantI4(offset, module.Definition));
				instructions.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
			}
			else if (size == 1)
			{
				LoadValue(instructions, index);
				instructions.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
			}
			else
			{
				LoadValue(instructions, index);
				instructions.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
				LoadVariable(instructions, new ConstantI4(size, module.Definition));
				instructions.Add(Instruction.FromOpCode(CilOpCodes.Mul));
			}
			instructions.Add(Instruction.FromOpCode(CilOpCodes.Add));
		}
		else if (isConstant && constantValue == 1)
		{
			instructions.Add(new SizeOfInstruction(elementTypeSignature));
			instructions.Add(Instruction.FromOpCode(CilOpCodes.Add));
		}
		else
		{
			LoadValue(instructions, index);
			instructions.Add(Instruction.FromOpCode(CilOpCodes.Conv_I));
			instructions.Add(new SizeOfInstruction(elementTypeSignature));
			instructions.Add(Instruction.FromOpCode(CilOpCodes.Mul));
			instructions.Add(Instruction.FromOpCode(CilOpCodes.Add));
		}
	}

	private IVariable LoadVariadicArguments(BasicBlock instructions, ReadOnlySpan<LLVMValueRef> variadicArguments, ModuleContext module)
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
		instructions.Add(new InitializeInstruction(intPtrBufferLocal));

		LocalVariable[] variadicLocals = new LocalVariable[variadicArguments.Length];
		for (int i = 0; i < variadicArguments.Length; i++)
		{
			LLVMValueRef variadicArgument = variadicArguments[i];
			LoadValue(instructions, variadicArgument);
			LocalVariable local = new LocalVariable(module.GetTypeSignature(variadicArgument));
			instructions.Add(new StoreVariableInstruction(local));
			variadicLocals[i] = local;
		}

		LocalVariable intPtrSpanLocal = new LocalVariable(intPtrSpan);
		instructions.Add(new AddressOfInstruction(intPtrBufferLocal));
		Call(instructions, inlineArrayAsSpan.MakeGenericInstanceMethod(intPtrBuffer.ToTypeSignature(), intPtr));
		instructions.Add(new StoreVariableInstruction(intPtrSpanLocal));

		for (int i = 0; i < variadicLocals.Length; i++)
		{
			instructions.Add(new AddressOfInstruction(intPtrSpanLocal));
			instructions.Add(new LoadVariableInstruction(new ConstantI4(i, module.Definition)));
			instructions.Add(new CallInstruction(intPtrSpanGetItem));
			instructions.Add(new AddressOfInstruction(variadicLocals[i]));
			instructions.Add(new StoreIndirectInstruction(intPtr));
		}

		LocalVariable intPtrReadOnlySpanLocal = new LocalVariable(intPtrReadOnlySpan);
		instructions.Add(new LoadVariableInstruction(intPtrSpanLocal));
		instructions.Add(new CallInstruction(spanToReadOnly.MakeGenericInstanceMethod(intPtr)));
		instructions.Add(new StoreVariableInstruction(intPtrReadOnlySpanLocal));

		return intPtrReadOnlySpanLocal;
	}

	private TypeSignature GetTypeSignature(LLVMValueRef value) => module.GetTypeSignature(value);

	private TypeSignature GetTypeSignature(LLVMTypeRef type) => module.GetTypeSignature(type);

	private void LoadValue(BasicBlock instructions, LLVMValueRef value)
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
						LoadVariable(instructions, new ConstantI4((int)integer, module.Definition));
					}
					else if (operandType is { IntWidth: sizeof(long) * BitsPerByte })
					{
						LoadVariable(instructions, new ConstantI8(integer, module.Definition));
					}
					else if (operandType is { IntWidth: 2 * sizeof(long) * BitsPerByte })
					{
						LoadVariable(instructions, new ConstantI8(integer, module.Definition));
						MethodDefinition conversionMethod = typeSignature.Resolve()!.Methods.First(m =>
						{
							return m.Name == "op_Implicit" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType is CorLibTypeSignature { ElementType: ElementType.I8 };
						});
						Call(instructions, module.Definition.DefaultImporter.ImportMethod(conversionMethod));
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
					instructions.Add(new AddressOfInstruction(global));
				}
				break;
			case LLVMValueKind.LLVMGlobalAliasValueKind:
				{
					LLVMValueRef[] operands = value.GetOperands();
					if (operands.Length == 1)
					{
						LoadValue(instructions, operands[0]);
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
							LoadVariable(instructions, new ConstantR4((float)floatingPoint, module.Definition));
							break;
						case CorLibTypeSignature { ElementType: ElementType.R8 }:
							LoadVariable(instructions, new ConstantR8(floatingPoint, module.Definition));
							break;
						default:
							throw new NotSupportedException();
					}
				}
				break;
			case LLVMValueKind.LLVMConstantDataArrayValueKind:
				{
					TypeSignature typeSignature = GetTypeSignature(value.TypeOf);

					TypeDefinition inlineArrayType = (TypeDefinition)typeSignature.ToTypeDefOrRef();
					module.InlineArrayTypes[inlineArrayType].GetUltimateElementType(out TypeSignature elementType, out int elementCount);

					ReadOnlySpan<byte> data = LibLLVMSharp.ConstantDataArrayGetData(value);

					if (elementType is CorLibTypeSignature { ElementType: ElementType.I2 } && data.TryParseCharacterArray(out string? @string))
					{
						elementType = module.Definition.CorLibTypeFactory.Char;

						IMethodDescriptor toCharacterSpan = module.SpanHelperType.Methods
							.Single(m => m.Name == nameof(SpanHelper.ToCharacterSpan));

						LoadVariable(instructions, new ConstantString(@string, module.Definition));
						Call(instructions, toCharacterSpan);
					}
					else if (elementType is CorLibTypeSignature { ElementType: ElementType.I1 or ElementType.U1 })
					{
						elementType = module.Definition.CorLibTypeFactory.Byte;

						IMethodDefOrRef spanConstructor = (IMethodDefOrRef)module.Definition.DefaultImporter
							.ImportMethod(typeof(ReadOnlySpan<byte>).GetConstructor([typeof(void*), typeof(int)])!);

						FieldDefinition field = module.AddStoredDataField(data);

						instructions.Add(new LoadFieldAddressInstruction(field));
						instructions.Add(new LoadVariableInstruction(new ConstantI4(data.Length, module.Definition)));
						instructions.Add(new NewObjectInstruction(spanConstructor));
					}
					else
					{
						IMethodDefOrRef createSpan = (IMethodDefOrRef)module.Definition.DefaultImporter
							.ImportMethod(typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateSpan))!);
						IMethodDescriptor createSpanInstance = createSpan.MakeGenericInstanceMethod(elementType);

						FieldDefinition field = module.AddStoredDataField(data);

						instructions.Add(new LoadTokenInstruction(field));
						instructions.Add(new CallInstruction(createSpanInstance));
					}

					Debug.Assert(elementType is not PointerTypeSignature, "Pointers cannot be used as generic type arguments");

					IMethodDescriptor createInlineArray = module.InlineArrayHelperType.Methods
						.Single(m => m.Name == nameof(InlineArrayHelper.Create))
						.MakeGenericInstanceMethod(typeSignature, elementType);

					Call(instructions, createInlineArray);
				}
				break;
			case LLVMValueKind.LLVMConstantArrayValueKind:
				{
					TypeSignature underlyingType = GetTypeSignature(value.TypeOf);

					LLVMValueRef[] elements = value.GetOperands();

					module.InlineArrayTypes[(TypeDefinition)underlyingType.ToTypeDefOrRef()].GetElementType(out TypeSignature elementType, out int elementCount);

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

					instructions.Add(new InitializeInstruction(spanLocal));

					instructions.Add(new AddressOfInstruction(bufferLocal));
					instructions.Add(new CallInstruction(inlineArrayAsSpan));
					instructions.Add(new StoreVariableInstruction(spanLocal));

					for (int i = 0; i < elements.Length; i++)
					{
						LLVMValueRef element = elements[i];
						instructions.Add(new AddressOfInstruction(spanLocal));
						LoadVariable(instructions, new ConstantI4(i, module.Definition));
						Call(instructions, getItem);
						LoadValue(instructions, element);
						instructions.Add(new StoreIndirectInstruction(elementType));
					}

					instructions.Add(new LoadVariableInstruction(bufferLocal));
				}
				break;
			case LLVMValueKind.LLVMConstantStructValueKind:
				{
					TypeSignature typeSignature = GetTypeSignature(value.TypeOf);
					TypeDefinition typeDefinition = (TypeDefinition)typeSignature.ToTypeDefOrRef();

					LLVMValueRef[] fields = value.GetOperands();
					if (fields.Length != typeDefinition.Fields.Count)
					{
						throw new Exception("Struct field count mismatch");
					}

					LocalVariable resultLocal = new(typeSignature);

					instructions.Add(new InitializeInstruction(resultLocal));

					for (int i = 0; i < fields.Length; i++)
					{
						LLVMValueRef field = fields[i];
						FieldDefinition fieldDefinition = typeDefinition.Fields[i];

						instructions.Add(new AddressOfInstruction(resultLocal));
						instructions.Add(new LoadFieldAddressInstruction(fieldDefinition));
						LoadValue(instructions, field);
						instructions.Add(new StoreIndirectInstruction(fieldDefinition.Signature!.FieldType));
					}

					LoadVariable(instructions, resultLocal);
				}
				break;
			case LLVMValueKind.LLVMConstantPointerNullValueKind:
			case LLVMValueKind.LLVMConstantAggregateZeroValueKind:
			case LLVMValueKind.LLVMUndefValueValueKind:
				{
					TypeSignature typeSignature = GetTypeSignature(value.TypeOf);
					LoadVariable(instructions, new DefaultVariable(typeSignature));
				}
				break;
			case LLVMValueKind.LLVMFunctionValueKind:
				{
					LoadVariable(instructions, new FunctionPointerVariable(module.Methods[value]));
				}
				break;
			case LLVMValueKind.LLVMConstantExprValueKind:
				{
					TypeSignature resultType = module.GetTypeSignature(value);
					if (resultType is not CorLibTypeSignature { ElementType: ElementType.Void })
					{
						LocalVariable resultVariable = new(resultType);
						instructionResults.Add(value, resultVariable);
						AddInstruction(instructions, value);
						LoadVariable(instructions, resultVariable);
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
					LoadVariable(instructions, instructionResults[value]);
				}
				break;
			case LLVMValueKind.LLVMArgumentValueKind:
				{
					LoadVariable(instructions, module.Methods[value.ParamParent].ParameterLookup[value]);
				}
				break;
			case LLVMValueKind.LLVMMetadataAsValueValueKind:
				{
					//Metadata is not a real type, so we just use Object. Anywhere metadata is supposed to be loaded, we instead load a null value.
					TypeSignature typeSignature = module.Definition.CorLibTypeFactory.Object;
					LoadVariable(instructions, new DefaultVariable(typeSignature));
				}
				break;
			default:
				throw new NotImplementedException(value.Kind.ToString());
		}
	}

	private void StoreResult(IList<Instruction> instructions, LLVMValueRef instruction)
	{
		instructions.Add(new StoreVariableInstruction(instructionResults[instruction]));
	}

	private void MaybeStoreResult(IList<Instruction> instructions, LLVMValueRef instruction)
	{
		if (instructionResults.TryGetValue(instruction, out LocalVariable? result))
		{
			instructions.Add(new StoreVariableInstruction(result));
		}
	}

	private static void LoadVariable(IList<Instruction> instructions, IVariable variable)
	{
		instructions.Add(new LoadVariableInstruction(variable));
	}

	private static void Call(IList<Instruction> instructions, IMethodDescriptor method)
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
	private void ConditionalBranch(BasicBlock instructions, LLVMBasicBlockRef trueBlock, LLVMBasicBlockRef falseBlock)
	{
		if (!trueBlock.StartsWithPhi())
		{
			// The true block does not start with a phi instruction.
			// The false block might or might not start with a phi instruction.

			instructions.Add(new BranchIfTrueInstruction(basicBlocks[trueBlock]));

			Branch(instructions, falseBlock);
		}
		else if (!falseBlock.StartsWithPhi())
		{
			// Only the true block starts with a phi instruction.

			instructions.Add(new BranchIfFalseInstruction(basicBlocks[falseBlock]));

			Branch(instructions, trueBlock);
		}
		else
		{
			// Both target blocks start with a phi instruction.
			// We arbitrarily create a helper basic block to handle the true case.

			BasicBlock helperBasicBlock = new();
			basicBlockList.Add(helperBasicBlock);

			instructions.Add(new BranchIfTrueInstruction(helperBasicBlock));

			Branch(instructions, falseBlock);

			Branch(helperBasicBlock, basicBlockRefs[instructions], trueBlock);
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
