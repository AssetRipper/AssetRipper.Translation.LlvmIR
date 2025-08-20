using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Instructions;
using LLVMSharp.Interop;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
			typeof(MightThrowAttribute),
			typeof(ExceptionInfo),
			typeof(StackFrame),
			typeof(StackFrameList),
			typeof(FatalException),
			typeof(PointerIndices),
			typeof(NativeMemoryHelper),
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

	public TypeSignature GetTypeSignature(LLVMValueRef value)
	{
		return value.Kind switch
		{
			LLVMValueKind.LLVMInstructionValueKind => Methods[value.InstructionParent.Parent].InstructionLookup[value].ResultTypeSignature,
			LLVMValueKind.LLVMArgumentValueKind => Methods[value.ParamParent].ParameterLookup[value].Definition.ParameterType,
			LLVMValueKind.LLVMGlobalVariableValueKind => GlobalVariables[value].PointerType,
			_ => GetTypeSignature(value.TypeOf),
		};
	}

	public void LoadValue(CilInstructionCollection CilInstructions, LLVMValueRef value)
	{
		LoadValue(CilInstructions, value, out _);
	}
	public void LoadValue(CilInstructionCollection instructions, LLVMValueRef value, out TypeSignature typeSignature)
	{
		switch (value.Kind)
		{
			case LLVMValueKind.LLVMConstantIntValueKind:
				{
					const int BitsPerByte = 8;
					long integer = value.ConstIntSExt;
					LLVMTypeRef operandType = value.TypeOf;
					typeSignature = GetTypeSignature(operandType);
					if (integer is <= int.MaxValue and >= int.MinValue && operandType is { IntWidth: <= sizeof(int) * BitsPerByte })
					{
						instructions.Add(CilOpCodes.Ldc_I4, (int)integer);
					}
					else if (operandType is { IntWidth: sizeof(long) * BitsPerByte })
					{
						instructions.Add(CilOpCodes.Ldc_I8, integer);
					}
					else if (operandType is { IntWidth: 2 * sizeof(long) * BitsPerByte })
					{
						instructions.Add(CilOpCodes.Ldc_I8, integer);
						MethodDefinition conversionMethod = typeSignature.Resolve()!.Methods.First(m =>
						{
							return m.Name == "op_Implicit" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType is CorLibTypeSignature { ElementType: ElementType.I8 };
						});
						instructions.Add(CilOpCodes.Call, Definition.DefaultImporter.ImportMethod(conversionMethod));
					}
					else
					{
						throw new NotSupportedException($"Unsupported integer type: {typeSignature}");
					}
				}
				break;
			case LLVMValueKind.LLVMGlobalVariableValueKind:
				{
					GlobalVariableContext global = GlobalVariables[value];

					global.AddLoadPointer(instructions);

					typeSignature = global.PointerType;
				}
				break;
			case LLVMValueKind.LLVMGlobalAliasValueKind:
				{
					LLVMValueRef[] operands = value.GetOperands();
					if (operands.Length == 1)
					{
						LoadValue(instructions, operands[0], out typeSignature);
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
					typeSignature = GetTypeSignature(value.TypeOf);
					switch (typeSignature)
					{
						case CorLibTypeSignature { ElementType: ElementType.R4 }:
							instructions.Add(CilOpCodes.Ldc_R4, (float)floatingPoint);
							break;
						case CorLibTypeSignature { ElementType: ElementType.R8 }:
							instructions.Add(CilOpCodes.Ldc_R8, floatingPoint);
							break;
						default:
							throw new NotSupportedException();
					}
				}
				break;
			case LLVMValueKind.LLVMConstantDataArrayValueKind:
				{
					typeSignature = GetTypeSignature(value.TypeOf);

					TypeDefinition inlineArrayType = (TypeDefinition)typeSignature.ToTypeDefOrRef();
					InlineArrayTypes[inlineArrayType].GetUltimateElementType(out TypeSignature elementType, out int elementCount);

					ReadOnlySpan<byte> data = LibLLVMSharp.ConstantDataArrayGetData(value);

					if (elementType is CorLibTypeSignature { ElementType: ElementType.I2 } && data.TryParseCharacterArray(out string? @string))
					{
						elementType = Definition.CorLibTypeFactory.Char;

						IMethodDescriptor toCharacterSpan = SpanHelperType.Methods
							.Single(m => m.Name == nameof(SpanHelper.ToCharacterSpan));

						instructions.Add(CilOpCodes.Ldstr, @string);
						instructions.Add(CilOpCodes.Call, toCharacterSpan);
					}
					else if (elementType is CorLibTypeSignature { ElementType: ElementType.I1 or ElementType.U1 })
					{
						elementType = Definition.CorLibTypeFactory.Byte;

						IMethodDefOrRef spanConstructor = (IMethodDefOrRef)Definition.DefaultImporter
							.ImportMethod(typeof(ReadOnlySpan<byte>).GetConstructor([typeof(void*), typeof(int)])!);

						FieldDefinition field = AddStoredDataField(data.ToArray());
						instructions.Add(CilOpCodes.Ldsflda, field);
						instructions.Add(CilOpCodes.Ldc_I4, data.Length);
						instructions.Add(CilOpCodes.Newobj, spanConstructor);
					}
					else
					{
						IMethodDefOrRef createSpan = (IMethodDefOrRef)Definition.DefaultImporter
							.ImportMethod(typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateSpan))!);
						IMethodDescriptor createSpanInstance = createSpan.MakeGenericInstanceMethod(elementType);

						FieldDefinition field = AddStoredDataField(data.ToArray());
						instructions.Add(CilOpCodes.Ldtoken, field);
						instructions.Add(CilOpCodes.Call, createSpanInstance);
					}

					Debug.Assert(elementType is not PointerTypeSignature, "Pointers cannot be used as generic type arguments");

					IMethodDescriptor createInlineArray = InlineArrayHelperType.Methods
						.Single(m => m.Name == nameof(InlineArrayHelper.Create))
						.MakeGenericInstanceMethod(typeSignature, elementType);

					instructions.Add(CilOpCodes.Call, createInlineArray);
				}
				break;
			case LLVMValueKind.LLVMConstantArrayValueKind:
				{
					TypeSignature underlyingType = GetTypeSignature(value.TypeOf);

					LLVMValueRef[] elements = value.GetOperands();

					InlineArrayTypes[(TypeDefinition)underlyingType.ToTypeDefOrRef()].GetElementType(out TypeSignature elementType, out int elementCount);

					if (elementCount != elements.Length)
					{
						throw new Exception("Array element count mismatch");
					}

					Debug.Assert(elementType is not PointerTypeSignature, "Pointers cannot be used as generic type arguments");

					TypeSignature spanType = Definition.DefaultImporter
						.ImportType(typeof(Span<>))
						.MakeGenericInstanceType(elementType);

					IMethodDescriptor inlineArrayAsSpan = InlineArrayHelperType.Methods
						.Single(m => m.Name == nameof(InlineArrayHelper.AsSpan))
						.MakeGenericInstanceMethod(underlyingType, elementType);

					MethodSignature getItemSignature = MethodSignature.CreateInstance(new GenericParameterSignature(GenericParameterType.Type, 0).MakeByReferenceType(), Definition.CorLibTypeFactory.Int32);
					IMethodDescriptor getItem = new MemberReference(spanType.ToTypeDefOrRef(), "get_Item", getItemSignature);

					CilLocalVariable bufferLocal = instructions.AddLocalVariable(underlyingType);
					CilLocalVariable spanLocal = instructions.AddLocalVariable(spanType);

					instructions.AddDefaultValue(underlyingType);
					instructions.Add(CilOpCodes.Stloc, bufferLocal);

					instructions.Add(CilOpCodes.Ldloca, bufferLocal);
					instructions.Add(CilOpCodes.Call, inlineArrayAsSpan);
					instructions.Add(CilOpCodes.Stloc, spanLocal);

					for (int i = 0; i < elements.Length; i++)
					{
						LLVMValueRef element = elements[i];
						instructions.Add(CilOpCodes.Ldloca, spanLocal);
						instructions.Add(CilOpCodes.Ldc_I4, i);
						instructions.Add(CilOpCodes.Call, getItem);
						LoadValue(instructions, element);
						instructions.AddStoreIndirect(elementType);
					}

					instructions.Add(CilOpCodes.Ldloc, bufferLocal);

					typeSignature = underlyingType;
				}
				break;
			case LLVMValueKind.LLVMConstantStructValueKind:
				{
					typeSignature = GetTypeSignature(value.TypeOf);
					TypeDefinition typeDefinition = (TypeDefinition)typeSignature.ToTypeDefOrRef();

					LLVMValueRef[] fields = value.GetOperands();
					if (fields.Length != typeDefinition.Fields.Count)
					{
						throw new Exception("Struct field count mismatch");
					}

					CilLocalVariable resultLocal = instructions.AddLocalVariable(typeSignature);

					instructions.AddDefaultValue(typeSignature);
					instructions.Add(CilOpCodes.Stloc, resultLocal);

					for (int i = 0; i < fields.Length; i++)
					{
						LLVMValueRef field = fields[i];
						FieldDefinition fieldDefinition = typeDefinition.Fields[i];
						instructions.Add(CilOpCodes.Ldloca, resultLocal);
						instructions.Add(CilOpCodes.Ldflda, fieldDefinition);
						LoadValue(instructions, field);
						instructions.AddStoreIndirect(fieldDefinition.Signature!.FieldType);
					}

					instructions.Add(CilOpCodes.Ldloc, resultLocal);
				}
				break;
			case LLVMValueKind.LLVMConstantPointerNullValueKind:
			case LLVMValueKind.LLVMConstantAggregateZeroValueKind:
			case LLVMValueKind.LLVMUndefValueValueKind:
				{
					typeSignature = GetTypeSignature(value.TypeOf);
					instructions.AddDefaultValue(typeSignature);
				}
				break;
			case LLVMValueKind.LLVMFunctionValueKind:
				{
					typeSignature = GetTypeSignature(value.TypeOf);

					Methods[value].AddLoadFunctionPointer(instructions);
				}
				break;
			case LLVMValueKind.LLVMConstantExprValueKind:
				{
					typeSignature = GetTypeSignature(value.TypeOf);
					InstructionContext expression = InstructionContext.Create(value, this);
					expression.CreateLocal(instructions);
					expression.AddInstructions(instructions);
					expression.AddLoad(instructions);
				}
				break;
			case LLVMValueKind.LLVMInstructionValueKind:
				{
					InstructionContext instruction = Methods[value.InstructionParent.Parent].InstructionLookup[value];
					instruction.AddLoad(instructions);
					typeSignature = instruction.ResultTypeSignature;
				}
				break;
			case LLVMValueKind.LLVMArgumentValueKind:
				{
					Parameter parameter = Methods[value.ParamParent].ParameterLookup[value].Definition;
					instructions.Add(CilOpCodes.Ldarg, parameter);
					typeSignature = parameter.ParameterType;
				}
				break;
			case LLVMValueKind.LLVMMetadataAsValueValueKind:
				{
					//Metadata is not a real type, so we just use Object. Anywhere metadata is supposed to be loaded, we instead load a null value.
					instructions.Add(CilOpCodes.Ldnull);
					typeSignature = Definition.CorLibTypeFactory.Object;
				}
				break;
			default:
				throw new NotImplementedException(value.Kind.ToString());
		}
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
	public FieldDefinition AddStoredDataField(byte[] data)
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
			privateImplementationField.FieldRva = new DataSegment(data);
			privateImplementationField.HasFieldRva = true;
			AddCompilerGeneratedAttribute(privateImplementationField);

			PrivateImplementationDetails.Fields.Add(privateImplementationField);
			storedDataFieldCache.Add(fieldName, privateImplementationField);
		}

		return privateImplementationField;

		//This might not be the correct way to choose a field name, but I think the specification allows it.
		//In any case, ILSpy handles it the way we want, which is all that matters.
		static string HashDataToBase64(byte[] data)
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

	private static TypeDefinition InjectType(Type type, ModuleDefinition targetModule)
	{
		ModuleDefinition executingModule = ModuleDefinition.FromFile(type.Assembly.Location);
		MemberCloner cloner = new(targetModule);
		cloner.Include(executingModule.TopLevelTypes.First(t => t.Namespace == type.Namespace && t.Name == type.Name));
		TypeDefinition result = cloner.Clone().ClonedTopLevelTypes.Single();
		result.Namespace = null;
		targetModule.TopLevelTypes.Add(result);
		return result;
	}
}
