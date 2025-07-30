using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Instructions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.LlvmIR;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class FunctionContext : IHasName
{
	private FunctionContext(LLVMValueRef function, MethodDefinition definition, ModuleContext module)
	{
		Function = function;
		Definition = definition;
		Module = module;

		MangledName = Function.Name;
		DemangledName = LibLLVMSharp.ValueGetDemangledName(function);
		CleanName = ExtractCleanName(MangledName, DemangledName, module.Options.RenamedSymbols, module.Options.ParseDemangledSymbols);
	}

	public static FunctionContext Create(LLVMValueRef function, ModuleContext module)
	{
		TypeDefinition declaringType = new(module.Options.GetNamespace("GlobalFunctions"), null, TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed, module.Definition.CorLibTypeFactory.Object.ToTypeDefOrRef());
		module.Definition.TopLevelTypes.Add(declaringType);

		MethodSignature signature = MethodSignature.CreateStatic(null!);
		MethodDefinition definition = new("Invoke", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, signature);
		definition.CilMethodBody = new(definition);
		declaringType.Methods.Add(definition);

		Debug.Assert(definition.Signature is not null);
		FunctionContext context = new(function, definition, module);
		module.Methods.Add(function, context);

		definition.Signature.ReturnType = context.ReturnTypeSignature;

		LLVMValueRef[] normalParameterRefs = function.GetParams();
		ParameterContext[] normalParameterContexts = new ParameterContext[normalParameterRefs.Length];
		for (int i = 0; i < normalParameterRefs.Length; i++)
		{
			LLVMValueRef parameter = normalParameterRefs[i];
			ParameterContext parameterContext = new(parameter, definition.AddParameter(module.Definition.CorLibTypeFactory.Object), context);
			normalParameterContexts[i] = parameterContext;
			context.ParameterLookup[parameter] = parameterContext;
		}

		if (context.IsVariadic)
		{
			context.VariadicParameter = new(definition.AddParameter(module.Definition.CorLibTypeFactory.Object), context);
		}

		context.NormalParameters = normalParameterContexts;

		context.AllParameters.AssignNames();
		foreach (BaseParameterContext parameter in context.AllParameters)
		{
			parameter.Definition.GetOrCreateDefinition().Name = parameter.Name;
		}

		// Pointer
		{
			TypeSignature voidPointerType = module.Definition.CorLibTypeFactory.Void.MakePointerType();

			FieldDefinition pointerField = new("__pointer", FieldAttributes.Private | FieldAttributes.Static, voidPointerType);
			declaringType.Fields.Add(pointerField);

			MethodDefinition getMethod = new("get_Pointer", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(voidPointerType));
			declaringType.Methods.Add(getMethod);
			getMethod.CilMethodBody = new(getMethod);

			CilInstructionCollection instructions = getMethod.CilMethodBody.Instructions;
			CilInstructionLabel notNullLabel = new();

			instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Ldc_I4_0);
			instructions.Add(CilOpCodes.Conv_U);
			instructions.Add(CilOpCodes.Bne_Un, notNullLabel);

			instructions.Add(CilOpCodes.Ldftn, definition);
			instructions.Add(CilOpCodes.Stsfld, pointerField);

			instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Call, module.InjectedTypes[typeof(PointerIndices)].GetMethodByName(nameof(PointerIndices.Register)));

			notNullLabel.Instruction = instructions.Add(CilOpCodes.Ldsfld, pointerField);
			instructions.Add(CilOpCodes.Ret);

			PropertyDefinition property = new("Pointer", PropertyAttributes.None, PropertySignature.CreateStatic(voidPointerType));
			declaringType.Properties.Add(property);
			property.GetMethod = getMethod;

			context.PointerField = pointerField;
			context.PointerGetMethod = getMethod;
			context.PointerProperty = property;
		}

		return context;
	}

	/// <inheritdoc/>
	public string MangledName { get; }
	/// <summary>
	/// The demangled name of the function, which might have signature information.
	/// </summary>
	public string? DemangledName { get; }
	/// <inheritdoc/>
	public string CleanName { get; }
	/// <inheritdoc/>
	public string Name { get; set; } = "";
	public bool MightThrowAnException { get; set; }
	public LLVMValueRef Function { get; }
	public unsafe bool IsVariadic => LLVM.IsFunctionVarArg(FunctionType) != 0;
	public LLVMTypeRef FunctionType => LibLLVMSharp.FunctionGetFunctionType(Function);
	public LLVMTypeRef ReturnType => LibLLVMSharp.FunctionGetReturnType(Function);
	public TypeSignature ReturnTypeSignature => Module.GetTypeSignature(ReturnType);
	public bool IsVoidReturn => ReturnType.Kind == LLVMTypeKind.LLVMVoidTypeKind;
	public FunctionContext? PersonalityFunction => Function.HasPersonalityFn
		? Module.Methods.TryGetValue(Function.PersonalityFn)
		: null;
	public bool IsIntrinsic => Function.BasicBlocksCount == 0;
	public ParameterContext[] NormalParameters { get; private set; } = [];
	public VariadicParameterContext? VariadicParameter { get; private set; }
	public IEnumerable<BaseParameterContext> AllParameters => VariadicParameter is null
		? NormalParameters
		: NormalParameters.Append<BaseParameterContext>(VariadicParameter);
	public AttributeWrapper[] Attributes => AttributeWrapper.FromArray(Function.GetAttributesAtIndex(LLVMAttributeIndex.LLVMAttributeFunctionIndex));
	public AttributeWrapper[] ReturnAttributes => AttributeWrapper.FromArray(Function.GetAttributesAtIndex(LLVMAttributeIndex.LLVMAttributeReturnIndex));
	public MethodDefinition Definition { get; }
	public TypeDefinition DeclaringType => Definition.DeclaringType!;
	public ModuleContext Module { get; }
	public List<BasicBlockContext> BasicBlocks { get; } = new();
	public List<InstructionContext> Instructions { get; } = new();
	public Dictionary<LLVMValueRef, ParameterContext> ParameterLookup { get; } = new();
	public Dictionary<LLVMValueRef, InstructionContext> InstructionLookup { get; } = new();
	public Dictionary<LLVMBasicBlockRef, BasicBlockContext> BasicBlockLookup { get; } = new();
	public TypeDefinition? LocalVariablesType { get; set; }
	public CilLocalVariable? StackFrameVariable { get; set; }
	private MethodDefinition PointerGetMethod { get; set; } = null!;
	private FieldDefinition PointerField { get; set; } = null!;
	private PropertyDefinition PointerProperty { get; set; } = null!;
	private bool IsPointerFieldUsed { get; set; } = false;

	public void InitializeInstructionData()
	{
		foreach (LLVMBasicBlockRef block in Function.GetBasicBlocks())
		{
			BasicBlockContext blockContext = BasicBlockContext.Create(block, this);
			BasicBlocks.Add(blockContext);
			BasicBlockLookup.Add(block, blockContext);
			Instructions.AddRange(blockContext.Instructions);
		}
		Instructions.EnsureCapacity(Instructions.Count);
		foreach (InstructionContext instruction in Instructions)
		{
			InstructionLookup.Add(instruction.Instruction, instruction);
		}
		foreach (BasicBlockContext basicBlock in BasicBlocks)
		{
			foreach (LLVMBasicBlockRef successor in basicBlock.Block.GetSuccessors())
			{
				BasicBlockContext successorBlock = BasicBlockLookup[successor];
				basicBlock.Successors.Add(successorBlock);
				successorBlock.Predecessors.Add(basicBlock);
			}
		}

	}

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

	public void ClearInstructionData()
	{
		Instructions.Clear();
		BasicBlocks.Clear();
		InstructionLookup.Clear();
		BasicBlockLookup.Clear();
	}

	public void AddLocalVariablesPointer(CilInstructionCollection instructions)
	{
		Debug.Assert(LocalVariablesType is not null);
		Debug.Assert(StackFrameVariable is not null);
		instructions.Add(CilOpCodes.Ldloca, StackFrameVariable);
		instructions.Add(CilOpCodes.Call, Module.InjectedTypes[typeof(StackFrame)].GetMethodByName(nameof(StackFrame.GetLocalsPointer)).MakeGenericInstanceMethod(LocalVariablesType.ToTypeSignature()));
	}

	public void AddLocalVariablesRef(CilInstructionCollection instructions)
	{
		Debug.Assert(LocalVariablesType is not null);
		Debug.Assert(StackFrameVariable is not null);
		instructions.Add(CilOpCodes.Ldloca, StackFrameVariable);
		instructions.Add(CilOpCodes.Call, Module.InjectedTypes[typeof(StackFrame)].GetMethodByName(nameof(StackFrame.GetLocalsRef)).MakeGenericInstanceMethod(LocalVariablesType.ToTypeSignature()));
	}

	public void AddLoadFunctionPointer(CilInstructionCollection instructions)
	{
		IsPointerFieldUsed = true;
		instructions.Add(CilOpCodes.Call, PointerGetMethod);
	}

	public void RemovePointerFieldIfNotUsed()
	{
		if (!IsPointerFieldUsed)
		{
			DeclaringType.Fields.Remove(PointerField);
			DeclaringType.Methods.Remove(PointerGetMethod);
			DeclaringType.Properties.Remove(PointerProperty);
			PointerField = null!;
			PointerGetMethod = null!;
			PointerProperty = null!;
		}
	}

	public void AddPublicImplementation()
	{
		MethodDefinition method = Definition;
		Debug.Assert(method.Signature is not null);

		MethodDefinition newMethod;
		CilInstructionCollection instructions;
		CilLocalVariable? returnLocal;
		if (TryGetStructReturnType(out TypeSignature? returnTypeSignature))
		{
			newMethod = new(Name, method.Attributes, MethodSignature.CreateStatic(returnTypeSignature, method.Signature.ParameterTypes.Skip(1)));
			Module.GlobalMembersType.Methods.Add(newMethod);
			newMethod.CilMethodBody = new(newMethod);

			instructions = newMethod.CilMethodBody.Instructions;
			returnLocal = instructions.AddLocalVariable(returnTypeSignature);
			instructions.InitializeDefaultValue(returnLocal);
			instructions.Add(CilOpCodes.Ldloca, returnLocal);
			foreach (Parameter parameter in newMethod.Parameters)
			{
				instructions.Add(CilOpCodes.Ldarg, parameter);
			}
			instructions.Add(CilOpCodes.Call, method);

			// Copy parameter names from the original method to the new method.
			for (int i = 0; i < newMethod.Parameters.Count; i++)
			{
				Parameter originalParameter = method.Parameters[i + 1];
				Parameter newParameter = newMethod.Parameters[i];
				Debug.Assert(originalParameter.Definition is not null);
				newParameter.GetOrCreateDefinition().Name = originalParameter.Definition.Name;
			}
		}
		else
		{
			newMethod = new(Name, method.Attributes, MethodSignature.CreateStatic(method.Signature.ReturnType, method.Signature.ParameterTypes));
			Module.GlobalMembersType.Methods.Add(newMethod);
			newMethod.CilMethodBody = new(newMethod);

			instructions = newMethod.CilMethodBody.Instructions;

			foreach (Parameter parameter in newMethod.Parameters)
			{
				instructions.Add(CilOpCodes.Ldarg, parameter);
			}
			instructions.Add(CilOpCodes.Call, method);

			// Copy parameter names from the original method to the new method.
			for (int i = 0; i < newMethod.Parameters.Count; i++)
			{
				Parameter originalParameter = method.Parameters[i];
				Parameter newParameter = newMethod.Parameters[i];
				Debug.Assert(originalParameter.Definition is not null);
				newParameter.GetOrCreateDefinition().Name = originalParameter.Definition.Name;
			}

			if (IsVoidReturn)
			{
				returnLocal = null;
			}
			else
			{
				returnLocal = instructions.AddLocalVariable(method.Signature.ReturnType);
				instructions.Add(CilOpCodes.Stloc, returnLocal);
			}
		}

		if (MightThrowAnException)
		{
			MethodDefinition exitToUserCode = Module.InjectedTypes[typeof(StackFrameList)].GetMethodByName(nameof(StackFrameList.ExitToUserCode));

			CilInstructionLabel returnLabel = new();

			ICilLabel tryStartLabel = instructions[0].CreateLabel();
			ICilLabel tryEndLabel = instructions.Add(CilOpCodes.Leave, returnLabel).CreateLabel();

			ICilLabel handlerStartLabel = instructions.Add(CilOpCodes.Pop).CreateLabel();
			instructions.Add(CilOpCodes.Call, exitToUserCode); // Clean up the stack frame.
			ICilLabel handlerEndLabel = instructions.Add(CilOpCodes.Rethrow).CreateLabel(); // Continue propagating the exception.

			returnLabel.Instruction = instructions.Add(CilOpCodes.Call, exitToUserCode); // Clean up the stack frame and maybe throw an exception.

			CilExceptionHandler exceptionHandler = new()
			{
				HandlerType = CilExceptionHandlerType.Exception,
				TryStart = tryStartLabel,
				TryEnd = tryEndLabel,
				HandlerStart = handlerStartLabel,
				HandlerEnd = handlerEndLabel,
				ExceptionType = Module.Definition.CorLibTypeFactory.Object.ToTypeDefOrRef(),
			};
			instructions.Owner.ExceptionHandlers.Add(exceptionHandler);
		}

		if (returnLocal is not null)
		{
			instructions.Add(CilOpCodes.Ldloc, returnLocal);
		}
		instructions.Add(CilOpCodes.Ret);
		instructions.OptimizeMacros();

		this.AddNameAttributes(newMethod);

		bool TryGetStructReturnType([NotNullWhen(true)] out TypeSignature? type)
		{
			if (IsVoidReturn && NormalParameters.Length > 0)
			{
				type = NormalParameters[0].StructReturnTypeSignature;
				return type is not null;
			}

			type = default;
			return false;
		}
	}

	private string GetDebuggerDisplay()
	{
		return Name;
	}

	private static string ExtractCleanName(string mangledName, string? demangledName, Dictionary<string, string> renamedSymbols, bool parseDemangledSymbols)
	{
		if (renamedSymbols.TryGetValue(mangledName, out string? result))
		{
			if (!NameGenerator.IsValidCSharpName(result))
			{
				throw new ArgumentException($"Renamed symbol '{mangledName}' has an invalid name '{result}'.", nameof(renamedSymbols));
			}
			return result;
		}

		if (parseDemangledSymbols && !string.IsNullOrEmpty(demangledName) && demangledName != mangledName && DemangledNamesParser.ParseFunction(demangledName, out string? returnType, out _, out string? typeName, out string? functionIdentifier, out string? functionName, out _, out _))
		{
			if (returnType is null && functionName == typeName)
			{
				result = NameGenerator.CleanName(typeName, "Type") + "_Constructor";
			}
			else if (returnType is null && functionName == $"~{typeName}")
			{
				result = NameGenerator.CleanName(typeName ?? "", "Type") + "_Destructor";
			}
			else if (returnType is "void *" && functionName == "`scalar deleting dtor'")
			{
				result = NameGenerator.CleanName(typeName ?? "", "Type") + "_Delete";
			}
			else
			{
				result = NameGenerator.CleanName(functionIdentifier, "Function");
			}
		}
		else
		{
			result = NameGenerator.CleanName(TryGetSimpleName(mangledName), "Function");
		}

		return result.CapitalizeGetOrSet();

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
