using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
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

		Debug.Assert(definition.Signature is not null);
		definition.Signature.ReturnType = ReturnTypeSignature;

		LLVMValueRef[] normalParameterRefs = function.GetParams();
		NormalParameters = new ParameterContext[normalParameterRefs.Length];
		for (int i = 0; i < normalParameterRefs.Length; i++)
		{
			LLVMValueRef parameter = normalParameterRefs[i];
			ParameterContext parameterContext = new(parameter, definition.AddParameter(module.Definition.CorLibTypeFactory.Object), this);
			NormalParameters[i] = parameterContext;
			ParameterLookup[parameter] = parameterContext;
		}

		if (IsVariadic)
		{
			VariadicParameter = new(definition.AddParameter(module.Definition.CorLibTypeFactory.Object), this);
		}

		if (module.Options.ParseDemangledSymbols && !string.IsNullOrEmpty(DemangledName) && DemangledName != MangledName && DemangledNamesParser.ParseFunction(DemangledName, out string? returnType, out _, out string? typeName, out string? functionIdentifier, out string? functionName, out _, out string[]? parameterTypes))
		{
			NativeType = returnType;

			if (ParameterCount == parameterTypes.Length)
			{
				int i = 0;
				foreach (BaseParameterContext parameter in AllParameters)
				{
					parameter.NativeType = parameterTypes[i];
					i++;
				}
			}
			else if (!string.IsNullOrEmpty(typeName) && ParameterCount - 1 == parameterTypes.Length)
			{
				int i = 0;
				foreach (BaseParameterContext parameter in AllParameters.Skip(1))
				{
					parameter.NativeType = parameterTypes[i];
					i++;
				}
			}
		}
		else
		{
			returnType = null;
			typeName = null;
			functionIdentifier = null;
			functionName = null;
		}

		AllParameters.AssignNames();
		foreach (BaseParameterContext parameter in AllParameters)
		{
			ParameterDefinition parameterDefinition = parameter.Definition.GetOrCreateDefinition();
			parameterDefinition.Name = parameter.Name;
			parameter.AddNameAndTypeAttributes(parameterDefinition);
		}

		if (module.Options.RenamedSymbols.TryGetValue(MangledName, out string? result))
		{
			if (!NameGenerator.IsValidCSharpName(result))
			{
				throw new ArgumentException($"Renamed symbol '{MangledName}' has an invalid name '{result}'.", nameof(module));
			}
		}
		else if (string.IsNullOrEmpty(functionIdentifier))
		{
			result = NameGenerator.CleanName(TryGetSimpleName(MangledName), "Function");
		}
		else if (string.IsNullOrEmpty(returnType) && !string.IsNullOrEmpty(typeName) && functionName == typeName)
		{
			result = NameGenerator.CleanName(typeName, "Type") + "_Constructor";
		}
		else if (string.IsNullOrEmpty(returnType) && functionName == $"~{typeName}")
		{
			result = NameGenerator.CleanName(typeName ?? "", "Type") + "_Destructor";
		}
		else if (returnType is "void *" && functionName == "`scalar deleting dtor'")
		{
			result = NameGenerator.CleanName(typeName ?? "", "Type") + "_Delete";
		}
		else if (functionIdentifier.StartsWith("operator", StringComparison.Ordinal))
		{
			string operatatorName = functionIdentifier switch
			{
				"operator==" => "Equals",
				"operator!=" => "NotEquals",
				"operator<" => "LessThan",
				"operator>" => "GreaterThan",
				"operator<=" => "LessThanOrEquals",
				"operator>=" => "GreaterThanOrEquals",
				"operator+" => "Add",
				"operator-" => "Subtract",
				"operator*" => "Multiply",
				"operator/" => "Divide",
				"operator%" => "Modulo",
				"operator&" => "BitwiseAnd",
				"operator|" => "BitwiseOr",
				"operator^" => "BitwiseXor",
				"operator~" => "BitwiseNot",
				"operator<<" => "LeftShift",
				"operator>>" => "RightShift",
				"operator->" => "PointerDereference",
				"operator++" => "Increment",
				"operator--" => "Decrement",
				"operator=" => "Assignment",
				"operator[]" => "Index",
				"operator()" => "Invoke",
				"operator bool" => "ToBoolean",
				"operator short" => "ToInt16",
				"operator int" => "ToInt32",
				"operator long long" => "ToInt64",
				"operator unsigned short" => "ToUInt16",
				"operator unsigned int" => "ToUInt32",
				"operator unsigned long long" => "ToUInt64",
				"operator float" => "ToSingle",
				"operator double" => "ToDouble",
				"operator new" => "New",
				"operator delete" => "Delete",
				"operator new[]" => "NewArray",
				"operator delete[]" => "DeleteArray",
				_ => NameGenerator.CleanName(functionIdentifier["operator".Length..], "Operator"),
			};
			if (string.IsNullOrEmpty(typeName))
			{
				result = operatatorName;
			}
			else
			{
				string cleanTypeName = NameGenerator.CleanName(typeName ?? "", "Type");
				result = $"{cleanTypeName}_{operatatorName}";
			}
		}
		else
		{
			result = NameGenerator.CleanName(functionIdentifier, "Function");
		}

		CleanName = result.CapitalizeGetOrSet();
	}

	public static FunctionContext Create(LLVMValueRef function, ModuleContext module)
	{
		TypeDefinition declaringType = new(module.Options.GetNamespace("GlobalFunctions"), null, TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, module.Definition.CorLibTypeFactory.Object.ToTypeDefOrRef());
		module.Definition.TopLevelTypes.Add(declaringType);

		MethodSignature signature = MethodSignature.CreateStatic(null!);
		MethodDefinition definition = new("Invoke", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, signature);
		definition.CilMethodBody = new();
		declaringType.Methods.Add(definition);

		FunctionContext context = new(function, definition, module);
		module.Methods.Add(function, context);

		// Pointer
		{
			TypeSignature voidPointerType = module.Definition.CorLibTypeFactory.Void.MakePointerType();

			FieldDefinition pointerField = new("__pointer", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, voidPointerType);
			declaringType.Fields.Add(pointerField);

			context.PointerField = pointerField;
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
	/// <inheritdoc/>
	public string? NativeType { get; set; }
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
	public int ParameterCount => NormalParameters.Length + (VariadicParameter is not null ? 1 : 0);
	public AttributeWrapper[] Attributes => AttributeWrapper.FromArray(Function.GetAttributesAtIndex(LLVMAttributeIndex.LLVMAttributeFunctionIndex));
	public AttributeWrapper[] ReturnAttributes => AttributeWrapper.FromArray(Function.GetAttributesAtIndex(LLVMAttributeIndex.LLVMAttributeReturnIndex));
	public MethodDefinition Definition { get; }
	public TypeDefinition DeclaringType => Definition.DeclaringType!;
	public ModuleContext Module { get; }
	public Dictionary<LLVMValueRef, ParameterContext> ParameterLookup { get; } = new();
	public bool NeedsStackFrame { get; set; }
	public TypeDefinition? LocalVariablesType { get; set; }
	public CilLocalVariable? StackFrameVariable { get; set; }
	private FieldDefinition PointerField { get; set; } = null!;
	private bool IsPointerFieldUsed { get; set; } = false;

	public BaseParameterContext GetParameter(int index)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(index);
		if (index < NormalParameters.Length)
		{
			return NormalParameters[index];
		}
		else if (index == NormalParameters.Length && VariadicParameter is not null)
		{
			return VariadicParameter;
		}
		else
		{
			throw new IndexOutOfRangeException();
		}
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
		instructions.Add(CilOpCodes.Ldsfld, PointerField);
	}

	public void RemovePointerFieldIfNotUsed()
	{
		if (!IsPointerFieldUsed)
		{
			DeclaringType.Fields.Remove(PointerField);
			PointerField = null!;
		}
		else
		{
			MethodDefinition staticConstructor = DeclaringType.GetOrCreateStaticConstructor();
			CilInstructionCollection instructions = staticConstructor.CilMethodBody!.Instructions;
			instructions.Clear();

			instructions.Add(CilOpCodes.Ldftn, Definition);
			instructions.Add(CilOpCodes.Call, Module.InjectedTypes[typeof(PointerIndices)].GetMethodByName(nameof(PointerIndices.Register)));
			instructions.Add(CilOpCodes.Stsfld, PointerField);
			instructions.Add(CilOpCodes.Ret);
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
			newMethod.CilMethodBody = new();

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
				ParameterDefinition newParameterDefinition = newParameter.GetOrCreateDefinition();
				newParameterDefinition.Name = originalParameter.Definition.Name;
				GetParameter(i + 1).AddNameAndTypeAttributes(newParameterDefinition);
			}
		}
		else
		{
			newMethod = new(Name, method.Attributes, MethodSignature.CreateStatic(method.Signature.ReturnType, method.Signature.ParameterTypes));
			Module.GlobalMembersType.Methods.Add(newMethod);
			newMethod.CilMethodBody = new();

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
				ParameterDefinition newParameterDefinition = newParameter.GetOrCreateDefinition();
				newParameterDefinition.Name = originalParameter.Definition.Name;
				GetParameter(i).AddNameAndTypeAttributes(newParameterDefinition);
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

		this.AddNameAndTypeAttributes(newMethod);

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

	private static string TryGetSimpleName(string name)
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
