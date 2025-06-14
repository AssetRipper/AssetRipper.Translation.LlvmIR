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

namespace AssetRipper.Translation.Cpp;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class FunctionContext : IHasName
{
	private FunctionContext(LLVMValueRef function, MethodDefinition definition, ModuleContext module)
	{
		Function = function;
		Parameters = function.GetParams();
		Definition = definition;
		Module = module;

		Attributes = AttributeWrapper.FromArray(function.GetAttributesAtIndex(LLVMAttributeIndex.LLVMAttributeFunctionIndex));
		ReturnAttributes = AttributeWrapper.FromArray(function.GetAttributesAtIndex(LLVMAttributeIndex.LLVMAttributeReturnIndex));
		ParameterAttributes = new AttributeWrapper[Parameters.Length][];
		for (int i = 0; i < Parameters.Length; i++)
		{
			ParameterAttributes[i] = AttributeWrapper.FromArray(function.GetAttributesAtIndex((LLVMAttributeIndex)(i + 1)));
		}

		DemangledName = LibLLVMSharp.ValueGetDemangledName(function);
		CleanName = ExtractCleanName(MangledName, module.Options.RenamedSymbols);
	}

	public static FunctionContext Create(LLVMValueRef function, MethodDefinition definition, ModuleContext module)
	{
		FunctionContext context = new(function, definition, module);
		module.Methods.Add(function, context);
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
		foreach (BasicBlockContext basicBlock in context.BasicBlocks)
		{
			context.Labels[basicBlock.Block] = new();
			foreach (LLVMBasicBlockRef successor in basicBlock.Block.GetSuccessors())
			{
				BasicBlockContext successorBlock = context.BasicBlockLookup[successor];
				basicBlock.Successors.Add(successorBlock);
				successorBlock.Predecessors.Add(basicBlock);
			}
			if (basicBlock.EndsWithInvoke)
			{
				InvokeInstructionContext instruction = (InvokeInstructionContext)basicBlock.Instructions[^1];
				BasicBlockContext? defaultBlock = instruction.DefaultBlock;
				if (defaultBlock is not null)
				{
					basicBlock.NormalSuccessors.Add(defaultBlock);
					defaultBlock.NormalPredecessors.Add(basicBlock);
				}
				BasicBlockContext? catchBlock = instruction.CatchBlock;
				if (catchBlock is not null)
				{
					basicBlock.InvokeSuccessors.Add(catchBlock);
					catchBlock.InvokePredecessors.Add(basicBlock);
				}
			}
			else if (basicBlock.IsCatchSwitch)
			{
				foreach (BasicBlockContext successor in basicBlock.Successors)
				{
					basicBlock.HandlerSuccessors.Add(successor);
					successor.HandlerPredecessors.Add(basicBlock);
				}
			}
			else if (basicBlock.EndsWithCatchReturn)
			{
				CatchReturnInstructionContext instruction = (CatchReturnInstructionContext)basicBlock.Instructions[^1];
				BasicBlockContext? catchBlock = instruction.TargetBlock;
				if (catchBlock is not null)
				{
					basicBlock.HandlerSuccessors.Add(catchBlock);
					catchBlock.HandlerPredecessors.Add(basicBlock);
				}
			}
			else if (basicBlock.EndsWithCleanupReturn)
			{
				CleanupReturnInstructionContext instruction = (CleanupReturnInstructionContext)basicBlock.Instructions[^1];
				BasicBlockContext? cleanupBlock = instruction.TargetBlock;
				if (cleanupBlock is not null)
				{
					basicBlock.HandlerSuccessors.Add(cleanupBlock);
					cleanupBlock.HandlerPredecessors.Add(basicBlock);
				}
			}
			else
			{
				foreach (BasicBlockContext successor in basicBlock.Successors)
				{
					basicBlock.NormalSuccessors.Add(successor);
					successor.NormalPredecessors.Add(basicBlock);
				}
			}
		}
		return context;
	}

	/// <inheritdoc/>
	public string MangledName => Function.Name;
	/// <summary>
	/// The demangled name of the function, which might have signature information.
	/// </summary>
	public string? DemangledName { get; }
	/// <inheritdoc/>
	public string CleanName { get; }
	/// <inheritdoc/>
	public string Name { get; set; } = "";
	public LLVMValueRef Function { get; }
	public unsafe bool IsVariadic => LLVM.IsFunctionVarArg(FunctionType) != 0;
	public LLVMTypeRef FunctionType => LibLLVMSharp.FunctionGetFunctionType(Function);
	public LLVMTypeRef ReturnType => LibLLVMSharp.FunctionGetReturnType(Function);
	public bool IsVoidReturn => ReturnType.Kind == LLVMTypeKind.LLVMVoidTypeKind;
	public LLVMValueRef[] Parameters { get; }
	public AttributeWrapper[] Attributes { get; }
	public AttributeWrapper[] ReturnAttributes { get; }
	public AttributeWrapper[][] ParameterAttributes { get; }
	public MethodDefinition Definition { get; }
	public ModuleContext Module { get; }
	public List<BasicBlockContext> BasicBlocks { get; } = new();
	public List<InstructionContext> Instructions { get; } = new();
	public Dictionary<LLVMBasicBlockRef, CilInstructionLabel> Labels { get; } = new();
	public Dictionary<LLVMValueRef, Parameter> ParameterDictionary { get; } = new();
	public Dictionary<LLVMValueRef, InstructionContext> InstructionLookup { get; } = new();
	public Dictionary<LLVMBasicBlockRef, BasicBlockContext> BasicBlockLookup { get; } = new();

	public void AnalyzeDataFlow()
	{
		foreach (InstructionContext instruction in Instructions)
		{
			switch (instruction)
			{
				case LoadInstructionContext loadInstructionContext:
					{
						loadInstructionContext.SourceInstruction = InstructionLookup.TryGetValue(loadInstructionContext.SourceOperand);
						loadInstructionContext.SourceInstruction?.Loads.Add(loadInstructionContext);
					}
					break;
				case StoreInstructionContext storeInstructionContext:
					{
						MaybeAddAccessor(storeInstructionContext, storeInstructionContext.SourceOperand);
						storeInstructionContext.DestinationInstruction = InstructionLookup.TryGetValue(storeInstructionContext.DestinationOperand);
						storeInstructionContext.DestinationInstruction?.Stores.Add(storeInstructionContext);
					}
					break;
				case PhiInstructionContext phiInstructionContext:
					{
						phiInstructionContext.InitializeIncomingBlocks();
						MaybeAddAccessors(phiInstructionContext, phiInstructionContext.Operands);
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

	public bool TryGetStructReturnType(out LLVMTypeRef type)
	{
		if (!IsVoidReturn || Parameters.Length == 0 || Parameters[0].TypeOf.Kind != LLVMTypeKind.LLVMPointerTypeKind)
		{
			type = default;
			return false;
		}

		AttributeWrapper[] parameter0Attributes = ParameterAttributes[0];
		for (int i = 0; i < parameter0Attributes.Length; i++)
		{
			AttributeWrapper attribute = parameter0Attributes[i];
			if (attribute.IsTypeAttribute) // Todo: Need to check the kind
			{
				type = attribute.TypeValue;
				return true;
			}
		}

		type = default;
		return false;
	}

	public void MaybeAddStructReturnMethod()
	{
		if (!TryGetStructReturnType(out LLVMTypeRef structReturnType))
		{
			return;
		}

		TypeSignature returnTypeSignature = Module.GetTypeSignature(structReturnType);

		MethodDefinition method = Definition;

		MethodDefinition newMethod = new(method.Name, method.Attributes, MethodSignature.CreateStatic(returnTypeSignature, method.Parameters.Skip(1).Select(p => p.ParameterType)));
		method.DeclaringType!.Methods.Add(newMethod);
		newMethod.CilMethodBody = new(newMethod);

		CilInstructionCollection instructions = newMethod.CilMethodBody.Instructions;
		CilLocalVariable returnLocal = instructions.AddLocalVariable(returnTypeSignature);
		instructions.InitializeDefaultValue(returnLocal);
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

	private string GetDebuggerDisplay()
	{
		return Name;
	}

	private static string ExtractCleanName(string mangledName, Dictionary<string, string> renamedSymbols)
	{
		if (renamedSymbols.TryGetValue(mangledName, out string? result))
		{
			if (!NameGenerator.IsValidCSharpName(result))
			{
				throw new ArgumentException($"Renamed symbol '{mangledName}' has an invalid name '{result}'.", nameof(renamedSymbols));
			}
			return result;
		}

		return NameGenerator.CleanName(TryGetSimpleName(mangledName), "Function");

		static string TryGetSimpleName(string name)
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
	}
}
