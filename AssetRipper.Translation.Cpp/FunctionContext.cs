using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class FunctionContext
{
	public FunctionContext(LLVMValueRef function, MethodDefinition definition, ModuleContext module)
	{
		Function = function;
		Parameters = function.GetParams();
		Definition = definition;
		Module = module;
	}

	public static FunctionContext Create(LLVMValueRef function, MethodDefinition definition, ModuleContext module)
	{
		FunctionContext context = new(function, definition, module);
		foreach (LLVMBasicBlockRef block in function.GetBasicBlocks())
		{
			BasicBlockContext blockContext = BasicBlockContext.Create(block, context);
			context.BasicBlocks.Add(blockContext);
			context.BasicBlockLookup.Add(block, blockContext);
			context.Instructions.AddRange(blockContext.Instructions);
		}
		context.Instructions.EnsureCapacity(context.Instructions.Count);
		foreach (InstructionContext instruction in context.Instructions)
		{
			context.InstructionLookup.Add(instruction.Instruction, instruction);
		}
		return context;
	}

	/// <summary>
	/// The name used from <see cref="Function"/>.
	/// </summary>
	public string MangledName => Function.Name;
	/// <summary>
	/// The demangled name of the function, which might have signature information.
	/// </summary>
	public string? DemangledName { get; set; }
	/// <summary>
	/// A clean name that might not be unique.
	/// </summary>
	public string CleanName { get; set; } = "";
	/// <summary>
	/// The unique name used for <see cref="Definition"/>.
	/// </summary>
	public string Name { get; set; } = "";
	public LLVMValueRef Function { get; }
	public LLVMTypeRef FunctionType => LibLLVMSharp.FunctionGetFunctionType(Function);
	public LLVMTypeRef ReturnType => LibLLVMSharp.FunctionGetReturnType(Function);
	public LLVMValueRef[] Parameters { get; }
	public MethodDefinition Definition { get; }
	public ModuleContext Module { get; }
	public List<BasicBlockContext> BasicBlocks { get; } = new();
	public List<InstructionContext> Instructions { get; } = new();
	public CilInstructionCollection CilInstructions => Definition.CilMethodBody!.Instructions;
	public Dictionary<LLVMValueRef, CilLocalVariable> InstructionLocals { get; } = new();
	/// <summary>
	/// Some local variables are simply pointers to other local variables, which hold the actual value.
	/// </summary>
	/// <remarks>
	/// Keys are the pointer locals, and the values are the data locals.
	/// </remarks>
	public Dictionary<CilLocalVariable, CilLocalVariable> DataLocals { get; } = new();
	public Dictionary<LLVMBasicBlockRef, CilInstructionLabel> Labels { get; } = new();
	public Dictionary<LLVMValueRef, Parameter> ParameterDictionary { get; } = new();
	public Dictionary<LLVMValueRef, InstructionContext> InstructionLookup { get; } = new();
	public Dictionary<LLVMBasicBlockRef, BasicBlockContext> BasicBlockLookup { get; } = new();

	public void LoadOperand(LLVMValueRef operand)
	{
		LoadOperand(operand, out _);
	}

	public void LoadOperand(LLVMValueRef operand, out TypeSignature typeSignature)
	{
		switch (operand.Kind)
		{
			case LLVMValueKind.LLVMConstantIntValueKind:
				{
					long value = operand.ConstIntSExt;
					LLVMTypeRef operandType = operand.TypeOf;
					if (value is <= int.MaxValue and >= int.MinValue && operandType is { IntWidth: <= sizeof(int) * 8 })
					{
						CilInstructions.Add(CilOpCodes.Ldc_I4, (int)value);
					}
					else
					{
						CilInstructions.Add(CilOpCodes.Ldc_I8, value);
					}
					typeSignature = Module.GetTypeSignature(operandType);
				}
				break;
			case LLVMValueKind.LLVMInstructionValueKind:
				{
					CilLocalVariable local = InstructionLocals[operand];
					CilInstructions.Add(CilOpCodes.Ldloc, local);
					typeSignature = local.VariableType;
				}
				break;
			case LLVMValueKind.LLVMArgumentValueKind:
				{
					Parameter parameter = ParameterDictionary[operand];
					CilInstructions.Add(CilOpCodes.Ldarg, parameter);
					typeSignature = parameter.ParameterType;
				}
				break;
			case LLVMValueKind.LLVMGlobalVariableValueKind:
				{
					FieldDefinition field = Module.GlobalConstants[operand];
					CilInstructions.Add(CilOpCodes.Ldsflda, field);
					typeSignature = Module.Definition.CorLibTypeFactory.Byte.MakePointerType();
				}
				break;
			case LLVMValueKind.LLVMConstantFPValueKind:
				{
					double value = operand.GetFloatingPointValue();
					typeSignature = Module.GetTypeSignature(operand.TypeOf);
					switch (typeSignature)
					{
						case CorLibTypeSignature { ElementType: ElementType.R4 }:
							CilInstructions.Add(CilOpCodes.Ldc_R4, (float)value);
							break;
						case CorLibTypeSignature { ElementType: ElementType.R8 }:
							CilInstructions.Add(CilOpCodes.Ldc_R8, value);
							break;
						default:
							throw new NotSupportedException();
					}
				}
				break;
			default:
				throw new NotSupportedException();
		}
	}

	public TypeSignature? GetOperandTypeSignature(LLVMValueRef operand)
	{
		return operand.Kind switch
		{
			LLVMValueKind.LLVMConstantIntValueKind or LLVMValueKind.LLVMConstantFPValueKind => Module.GetTypeSignature(operand.TypeOf),
			LLVMValueKind.LLVMInstructionValueKind => InstructionLookup[operand].ResultTypeSignature,
			LLVMValueKind.LLVMArgumentValueKind => null, //Parameters[operand].ParameterType,
			LLVMValueKind.LLVMGlobalVariableValueKind => Module.Definition.CorLibTypeFactory.Byte.MakePointerType(),
			_ => throw new NotSupportedException(),
		};
	}

	public void Analyze()
	{
		// Initial pass
		foreach (InstructionContext instruction in Instructions)
		{
			switch (instruction)
			{
				case AllocaInstructionContext allocaInstructionContext:
					{
						allocaInstructionContext.AllocatedTypeSignature = Module.GetTypeSignature(allocaInstructionContext.AllocatedType);
						allocaInstructionContext.InitializePointerTypeSignature();
						MaybeAddAccessor(allocaInstructionContext, allocaInstructionContext.SizeOperand);
					}
					break;
				case LoadInstructionContext loadInstructionContext:
					{
						loadInstructionContext.SourceInstruction = InstructionLookup[loadInstructionContext.SourceOperand];
						loadInstructionContext.SourceInstruction.Loads.Add(loadInstructionContext);

						//https://llvm.org/docs/OpaquePointers.html#migration-instructions
						LLVMTypeRef loadType = loadInstructionContext.Instruction.TypeOf;
						//if () //if not opaque ref
						{
							loadInstructionContext.ResultTypeSignature = Module.GetTypeSignature(loadType);
						}
					}
					break;
				case StoreInstructionContext storeInstructionContext:
					{
						MaybeAddAccessor(storeInstructionContext, storeInstructionContext.SourceOperand);
						storeInstructionContext.DestinationInstruction = InstructionLookup[storeInstructionContext.DestinationOperand];
						storeInstructionContext.DestinationInstruction.Stores.Add(storeInstructionContext);
					}
					break;
				case CallInstructionContext callInstructionContext:
					{
						callInstructionContext.FunctionCalled = Module.Methods[callInstructionContext.FunctionOperand];
						MaybeAddAccessors(callInstructionContext, callInstructionContext.ArgumentOperands);
					}
					break;
				case PhiInstructionContext phiInstructionContext:
					{
						phiInstructionContext.InitializeIncomingBlocks();
						MaybeAddAccessors(phiInstructionContext, phiInstructionContext.Operands);
					}
					break;
				case GetElementPointerInstructionContext gepInstructionContext:
					{
						gepInstructionContext.SourceElementTypeSignature = Module.GetTypeSignature(gepInstructionContext.SourceElementType);
						if (gepInstructionContext.SourceElementTypeSignature is not null)
						{
							gepInstructionContext.ResultTypeSignature = gepInstructionContext.CalculateFinalType().MakePointerType();
						}
						MaybeAddAccessors(gepInstructionContext, gepInstructionContext.Operands);
					}
					break;
				default:
					{
						MaybeAddAccessors(instruction, instruction.Operands);
					}
					break;
			}
		}

		void MaybeAddAccessors(InstructionContext instruction, ReadOnlySpan<LLVMValueRef> operands)
		{
			foreach (LLVMValueRef operand in operands)
			{
				MaybeAddAccessor(instruction, operand);
			}
		}
		void MaybeAddAccessor(InstructionContext instruction, LLVMValueRef operand)
		{
			if (InstructionLookup.TryGetValue(operand, out InstructionContext? source))
			{
				source.Accessors.Add(instruction);
			}
		}
	}

	public void CreateLabelsForBasicBlocks()
	{
		foreach (LLVMBasicBlockRef basicBlock in Function.GetBasicBlocks())
		{
			Labels[basicBlock] = new();
		}
	}

	private string GetDebuggerDisplay()
	{
		return DemangledName ?? Name;
	}
}
