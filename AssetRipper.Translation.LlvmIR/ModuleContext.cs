using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Attributes;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace AssetRipper.Translation.LlvmIR;

internal sealed partial class ModuleContext
{
	public ModuleContext(LLVMModuleRef module, ModuleDefinition definition, TranslatorOptions options)
	{
		HelpersNamespace = options.GetNamespace("Helpers");
		InjectedTypes = new TypeInjector(definition, HelpersNamespace).Inject(
		[
			typeof(IntrinsicFunctions),
			typeof(InlineArrayHelper),
			typeof(IInlineArray<>),
			typeof(SpanHelper),
			typeof(InstructionHelper),
			typeof(NumericHelper),
			typeof(InlineArrayNumericHelper),
			typeof(InstructionNotSupportedException),
			typeof(NameAttribute),
			typeof(MangledNameAttribute),
			typeof(DemangledNameAttribute),
			typeof(CleanNameAttribute),
			typeof(InlineAssemblyAttribute),
			typeof(MightThrowAttribute),
			typeof(ExceptionInfo),
			typeof(StackFrame),
			typeof(StackFrameList),
			typeof(FatalException),
			typeof(PointerIndices),
			typeof(NativeMemoryHelper),
			typeof(AssemblyFunctions),
		]);

		Module = module;
		Definition = definition;
		Options = options;
		GlobalMembersType = CreateStaticType(string.IsNullOrEmpty(options.ClassName) ? "GlobalMembers" : options.ClassName, true);

		CompilerGeneratedAttributeConstructor = (IMethodDefOrRef)definition.DefaultImporter.ImportMethod(typeof(CompilerGeneratedAttribute).GetConstructors()[0]);

		PrivateImplementationDetails = new TypeDefinition(null, "<PrivateImplementationDetails>", TypeAttributes.NotPublic | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed, Definition.CorLibTypeFactory.Object.ToTypeDefOrRef());
		AddCompilerGeneratedAttribute(PrivateImplementationDetails);
		Definition.TopLevelTypes.Add(PrivateImplementationDetails);
	}

	public string? HelpersNamespace { get; }
	public IReadOnlyDictionary<Type, TypeDefinition> InjectedTypes { get; }
	public TypeDefinition IntrinsicsType => InjectedTypes[typeof(IntrinsicFunctions)];
	public TypeDefinition InlineArrayHelperType => InjectedTypes[typeof(InlineArrayHelper)];
	public TypeDefinition InlineArrayInterface => InjectedTypes[typeof(IInlineArray<>)];
	public TypeDefinition SpanHelperType => InjectedTypes[typeof(SpanHelper)];
	public TypeDefinition InstructionHelperType => InjectedTypes[typeof(InstructionHelper)];
	public TypeDefinition NumericHelperType => InjectedTypes[typeof(NumericHelper)];
	public TypeDefinition InlineArrayNumericHelperType => InjectedTypes[typeof(InlineArrayNumericHelper)];
	public TypeDefinition InstructionNotSupportedExceptionType => InjectedTypes[typeof(InstructionNotSupportedException)];

	public LLVMModuleRef Module { get; }
	public ModuleDefinition Definition { get; }
	public TranslatorOptions Options { get; }
	public TypeDefinition GlobalMembersType { get; }
	public TypeDefinition PrivateImplementationDetails { get; }
	private Dictionary<string, FieldDefinition> storedDataFieldCache = new();
	private IMethodDefOrRef CompilerGeneratedAttributeConstructor { get; }
	public Dictionary<LLVMValueRef, FunctionContext> Methods { get; } = new();
	public Dictionary<LLVMTypeRef, StructContext> Structs { get; } = new();
	public Dictionary<LLVMValueRef, GlobalVariableContext> GlobalVariables { get; } = new();
	private readonly Dictionary<(TypeSignature, int), InlineArrayContext> inlineArrayCache = new(TypeSignatureIntPairComparer);
	public Dictionary<TypeDefinition, InlineArrayContext> InlineArrayTypes { get; } = new(SignatureComparer.Default);

	private static PairEqualityComparer<TypeSignature, int> TypeSignatureIntPairComparer { get; } = new(SignatureComparer.Default, EqualityComparer<int>.Default);

	public InlineArrayContext GetOrCreateInlineArray(TypeSignature type, int size)
	{
		if (type is PointerTypeSignature)
		{
			type = Definition.CorLibTypeFactory.IntPtr; // Pointers cannot be used as generic type arguments, so we use IntPtr instead.
		}
		(TypeSignature, int) pair = (type, size);
		if (!inlineArrayCache.TryGetValue(pair, out InlineArrayContext? arrayType))
		{
			arrayType = InlineArrayContext.CreateInlineArray(type, size, this);

			inlineArrayCache.Add(pair, arrayType);
			InlineArrayTypes.Add(arrayType.Type, arrayType);
		}

		return arrayType;
	}

	public InlineArrayContext GetContextForInlineArray(TypeSignature arrayType)
	{
		return InlineArrayTypes[(TypeDefinition)arrayType.ToTypeDefOrRef()];
	}

	public void CreateFunctions()
	{
		foreach (LLVMValueRef function in Module.GetFunctions())
		{
			FunctionContext.Create(function, this);
		}
	}

	public void AssignMemberNames()
	{
		Methods.Values.Concat<IHasName>(GlobalVariables.Values).AssignNames();
		foreach (FunctionContext functionContext in Methods.Values)
		{
			functionContext.DeclaringType.Name = functionContext.Name;
		}
	}

	public void AssignStructNames()
	{
		Structs.Values.AssignNames();
		foreach (StructContext structContext in Structs.Values)
		{
			structContext.AddNameAttributes();
		}
	}

	public void IdentifyFunctionsThatMightThrow()
	{
		HashSet<string> intrinsicMethodsThatMightThrow = new();
		foreach (MethodDefinition method in IntrinsicsType.Methods)
		{
			if (!method.HasCustomAttribute(HelpersNamespace, nameof(MightThrowAttribute)))
			{
				continue;
			}

			foreach (CustomAttribute attribute in method.FindCustomAttributes(HelpersNamespace, nameof(MangledNameAttribute)))
			{
				string? mangledName = attribute.Signature?.FixedArguments[0].Element?.ToString();
				if (mangledName is not null)
				{
					intrinsicMethodsThatMightThrow.Add(mangledName);
				}
			}
		}

		bool anyIntrinsicsUsedThatMightThrow = false;
		foreach (FunctionContext function in Methods.Values)
		{
			if (function.IsIntrinsic && intrinsicMethodsThatMightThrow.Contains(function.MangledName))
			{
				function.MightThrowAnException = true;
				anyIntrinsicsUsedThatMightThrow = true;
			}
		}

		if (!anyIntrinsicsUsedThatMightThrow && !Methods.Values.SelectMany(f => f.Function.GetInstructions()).Any(i => i.InstructionOpcode == LLVMOpcode.LLVMInvoke))
		{
			// If no intrinsic methods that might throw are used, and no invoke instructions are present,
			// so we can assume that no function pointer calls might throw exceptions.
			return;
		}

		bool changed;
		do
		{
			changed = false;

			foreach (FunctionContext function in Methods.Values)
			{
				if (function.MightThrowAnException)
				{
					continue;
				}

				foreach (LLVMValueRef instruction in function.Function.GetInstructions())
				{
					if (instruction.InstructionOpcode is not LLVMOpcode.LLVMInvoke and not LLVMOpcode.LLVMCall)
					{
						continue;
					}

					LLVMValueRef calledFunction = instruction.GetOperand((uint)(instruction.OperandCount - 1));
					if (calledFunction.IsAFunction != default)
					{
						if (Methods[calledFunction].MightThrowAnException)
						{
							function.MightThrowAnException = true;
							changed = true;
						}
					}
					else
					{
						function.MightThrowAnException = true;
						changed = true;
					}
				}
			}

		} while (changed);

		foreach (FunctionContext function in Methods.Values)
		{
			if (!function.MightThrowAnException)
			{
				continue;
			}

			function.NeedsStackFrame = function.Function.GetInstructions().Any(i => i.InstructionOpcode is LLVMOpcode.LLVMAlloca);
			if (!function.NeedsStackFrame)
			{
				continue;
			}

			TypeDefinition typeDefinition = new(
				null,
				"LocalVariables",
				TypeAttributes.NestedPrivate | TypeAttributes.SequentialLayout,
				Definition.DefaultImporter.ImportType(typeof(ValueType)));
			function.DeclaringType.NestedTypes.Add(typeDefinition);

			function.LocalVariablesType = typeDefinition;

			function.StackFrameVariable = function.Definition.CilMethodBody!.Instructions.AddLocalVariable(InjectedTypes[typeof(StackFrame)].ToTypeSignature());
		}
	}

	public TypeSignature GetTypeSignature(LLVMTypeRef type)
	{
		switch (type.Kind)
		{
			case LLVMTypeKind.LLVMVoidTypeKind:
				return Definition.CorLibTypeFactory.Void;

			case LLVMTypeKind.LLVMHalfTypeKind:
				return Definition.DefaultImporter.ImportTypeSignature(typeof(Half));

			case LLVMTypeKind.LLVMFloatTypeKind:
				return Definition.CorLibTypeFactory.Single;

			case LLVMTypeKind.LLVMDoubleTypeKind:
				return Definition.CorLibTypeFactory.Double;

			case LLVMTypeKind.LLVMX86_FP80TypeKind:
				//x86_fp80 has a very unique structure and uses 10 bytes
				goto default;

			case LLVMTypeKind.LLVMFP128TypeKind:
				//IEEE 754 floating point number with 128 bits.
				goto default;

			case LLVMTypeKind.LLVMPPC_FP128TypeKind:
				//ppc_fp128 can be approximated by fp128, which conforms to IEEE 754 standards.
				goto case LLVMTypeKind.LLVMFP128TypeKind;

			case LLVMTypeKind.LLVMLabelTypeKind:
				goto default;

			case LLVMTypeKind.LLVMIntegerTypeKind:
				return type.IntWidth switch
				{
					//Note: non-powers of 2 are valid and might be used for bitfields.
					1 => Definition.CorLibTypeFactory.Boolean,
					8 => Definition.CorLibTypeFactory.SByte,
					16 => Definition.CorLibTypeFactory.Int16,
					32 => Definition.CorLibTypeFactory.Int32,
					64 => Definition.CorLibTypeFactory.Int64,
					128 => Definition.DefaultImporter.ImportTypeSignature(typeof(Int128)),
					_ => throw new NotSupportedException(),
				};

			case LLVMTypeKind.LLVMFunctionTypeKind:
				//Function pointers are represented as void*, except at call sites.
				return Definition.CorLibTypeFactory.Void.MakePointerType();

			case LLVMTypeKind.LLVMStructTypeKind:
				{
					if (!Structs.TryGetValue(type, out StructContext? structContext))
					{
						structContext = StructContext.Create(this, type);
						Structs.Add(type, structContext);
					}
					return structContext.Definition.ToTypeSignature();
				}

			case LLVMTypeKind.LLVMArrayTypeKind:
				{
					TypeSignature elementType = GetTypeSignature(type.ElementType);
					int count = (int)type.ArrayLength;
					TypeDefinition arrayType = GetOrCreateInlineArray(elementType, count).Type;
					return arrayType.ToTypeSignature();
				}

			case LLVMTypeKind.LLVMPointerTypeKind:
				//All pointers are opaque in IR
				return Definition.CorLibTypeFactory.Void.MakePointerType();

			case LLVMTypeKind.LLVMVectorTypeKind:
			case LLVMTypeKind.LLVMScalableVectorTypeKind:
				unsafe
				{
					// Since we control the target platform, we can set vscale to 1.

					// https://github.com/dotnet/LLVMSharp/pull/235
					//TypeSignature elementType = GetTypeSignature(type.ElementType);
					//int count = (int)type.VectorSize;
					TypeSignature elementType = GetTypeSignature(LLVM.GetElementType(type));
					int count = (int)LLVM.GetVectorSize(type);
					TypeDefinition arrayType = GetOrCreateInlineArray(elementType, count).Type;
					return arrayType.ToTypeSignature();
				}

			case LLVMTypeKind.LLVMMetadataTypeKind:
				//Metadata is not a real type, so we just use Object. Anywhere metadata is supposed to be loaded, we instead load a null value.
				return Definition.CorLibTypeFactory.Object;

			case LLVMTypeKind.LLVMTokenTypeKind:
				return Definition.CorLibTypeFactory.Void;

			case LLVMTypeKind.LLVMBFloatTypeKind:
				//Half is just an approximation of BFloat16, which is not yet supported in .NET
				//Maybe we can use this instead: https://www.nuget.org/packages/UltimateOrb.TruncatedFloatingPoints
				return Definition.DefaultImporter.ImportTypeSignature(typeof(Half));

			case LLVMTypeKind.LLVMX86_AMXTypeKind:
				goto default;

			case LLVMTypeKind.LLVMTargetExtTypeKind:
				goto default;

			default:
				throw new NotImplementedException(type.Kind.ToString());
		}
	}

	public unsafe TypeSignature GetTypeSignature(LLVMValueRef value)
	{
		return value.Kind switch
		{
			LLVMValueKind.LLVMInstructionValueKind or LLVMValueKind.LLVMConstantExprValueKind => value.GetOpcode() switch
			{
				LLVMOpcode.LLVMAlloca => GetTypeSignature(LLVM.GetAllocatedType(value)).MakePointerType(),
				LLVMOpcode.LLVMCatchPad or LLVMOpcode.LLVMCleanupPad => InjectedTypes[typeof(ExceptionInfo)].ToTypeSignature(),
				LLVMOpcode.LLVMGetElementPtr => GetGEPFinalType(value).MakePointerType(),
				LLVMOpcode.LLVMRet => Definition.CorLibTypeFactory.Void,
				LLVMOpcode.LLVMStore => Definition.CorLibTypeFactory.Void,
				_ => GetTypeSignature(value.TypeOf),
			},
			LLVMValueKind.LLVMArgumentValueKind => Methods[value.ParamParent].ParameterLookup[value].Definition.ParameterType,
			LLVMValueKind.LLVMGlobalVariableValueKind => GlobalVariables[value].PointerType,
			_ => GetTypeSignature(value.TypeOf),
		};
	}

	private unsafe TypeSignature GetGEPFinalType(LLVMValueRef instruction)
	{
		Debug.Assert(instruction.GetOpcode() is LLVMOpcode.LLVMGetElementPtr);

		ReadOnlySpan<LLVMValueRef> otherIndices = instruction.GetOperands().AsSpan(2);

		LLVMTypeRef sourceElementType = LLVM.GetGEPSourceElementType(instruction);
		TypeSignature sourceElementTypeSignature = GetTypeSignature(sourceElementType);

		TypeSignature currentType = sourceElementTypeSignature;
		foreach (LLVMValueRef operand in otherIndices)
		{
			LLVMTypeRef operandType = operand.TypeOf;

			operandType.ThrowIfNotCoreLibInteger();

			TypeDefOrRefSignature structTypeSignature = (TypeDefOrRefSignature)currentType;
			TypeDefinition structType = (TypeDefinition)structTypeSignature.ToTypeDefOrRef();

			if (InlineArrayTypes.TryGetValue(structType, out InlineArrayContext? inlineArray))
			{
				currentType = inlineArray.ElementType;
			}
			else
			{
				Debug.Assert(operand.Kind == LLVMValueKind.LLVMConstantIntValueKind);

				int index = (int)operand.ConstIntSExt;
				FieldDefinition field = structType.GetInstanceField(index);
				currentType = field.Signature!.FieldType;
			}
		}

		return currentType;
	}

	private TypeDefinition CreateStaticType(string name, bool @public)
	{
		TypeDefinition typeDefinition = new(Options.GetNamespace(@public ? null : "Implementations"), name, (@public ? TypeAttributes.Public : TypeAttributes.NotPublic) | TypeAttributes.Abstract | TypeAttributes.Sealed, Definition.CorLibTypeFactory.Object.ToTypeDefOrRef());
		Definition.TopLevelTypes.Add(typeDefinition);
		return typeDefinition;
	}

	private void AddCompilerGeneratedAttribute(IHasCustomAttribute hasCustomAttribute)
	{
		CustomAttributeSignature attributeSignature = new();
		CustomAttribute attribute = new((ICustomAttributeType)CompilerGeneratedAttributeConstructor, attributeSignature);
		hasCustomAttribute.CustomAttributes.Add(attribute);
	}

	/// <summary>
	/// Adds a byte array field to the PrivateImplementationDetails class.
	/// </summary>
	/// <param name="fieldName">The name of the field.</param>
	/// <param name="data">The data contained within the field.</param>
	/// <returns>The field's <see cref="FieldDefinition"/>.</returns>
	public FieldDefinition AddStoredDataField(ReadOnlySpan<byte> data)
	{
		TypeDefinition nestedType = GetOrCreateStaticArrayInitType(data.Length);

		string fieldName = HashDataToBase64(data);
		TypeSignature fieldType = nestedType.ToTypeSignature();

		if (storedDataFieldCache.TryGetValue(fieldName, out FieldDefinition? privateImplementationField))
		{
			Debug.Assert(SignatureComparer.Default.Equals(privateImplementationField.Signature?.FieldType, fieldType));
		}
		else
		{
			privateImplementationField = new FieldDefinition(fieldName, FieldAttributes.Assembly | FieldAttributes.Static, fieldType);
			privateImplementationField.IsInitOnly = true;
			privateImplementationField.FieldRva = new DataSegment(data.ToArray());
			privateImplementationField.HasFieldRva = true;
			AddCompilerGeneratedAttribute(privateImplementationField);

			PrivateImplementationDetails.Fields.Add(privateImplementationField);
			storedDataFieldCache.Add(fieldName, privateImplementationField);
		}

		return privateImplementationField;

		//This might not be the correct way to choose a field name, but I think the specification allows it.
		//In any case, ILSpy handles it the way we want, which is all that matters.
		static string HashDataToBase64(ReadOnlySpan<byte> data)
		{
			byte[] hash = SHA256.HashData(data);
			return Convert.ToBase64String(hash, Base64FormattingOptions.None);
		}
	}

	private TypeDefinition GetOrCreateStaticArrayInitType(int length)
	{
		string name = $"__StaticArrayInitTypeSize={length}";

		foreach (TypeDefinition nestedType in PrivateImplementationDetails.NestedTypes)
		{
			if (nestedType.Name == name)
			{
				return nestedType;
			}
		}

		TypeDefinition result = new TypeDefinition(null, name,
			TypeAttributes.NestedPrivate |
			TypeAttributes.ExplicitLayout |
			TypeAttributes.AnsiClass |
			TypeAttributes.Sealed);
		PrivateImplementationDetails.NestedTypes.Add(result);

		result.BaseType = Definition.DefaultImporter.ImportType(typeof(ValueType));
		result.ClassLayout = new ClassLayout(1, (uint)length);
		AddCompilerGeneratedAttribute(result);

		return result;
	}
}
