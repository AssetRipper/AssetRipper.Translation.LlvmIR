using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.Cpp.Extensions;
using AssetRipper.Translation.Cpp.Instructions;
using LLVMSharp.Interop;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace AssetRipper.Translation.Cpp;

internal sealed class ModuleContext
{
	public ModuleContext(LLVMModuleRef module, ModuleDefinition definition)
	{
		Module = module;
		Definition = definition;
		GlobalFunctionsType = CreateStaticType(definition, "GlobalFunctions");
		PointerCacheType = CreateStaticType(definition, "PointerCache", false);
		GlobalVariablePointersType = CreateStaticType(definition, "GlobalVariablePointers", false);
		GlobalVariablesType = CreateStaticType(definition, "GlobalVariables");
		IntrinsicsType = InjectType(typeof(IntrinsicFunctions), definition);
		InlineArrayHelperType = InjectType(typeof(InlineArrayHelper), definition);

		CompilerGeneratedAttributeConstructor = (IMethodDefOrRef)definition.DefaultImporter.ImportMethod(typeof(CompilerGeneratedAttribute).GetConstructors()[0]);

		PrivateImplementationDetails = new TypeDefinition(null, "<PrivateImplementationDetails>", TypeAttributes.NotPublic | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed);
		AddCompilerGeneratedAttribute(PrivateImplementationDetails);
		Definition.TopLevelTypes.Add(PrivateImplementationDetails);
	}

	public LLVMModuleRef Module { get; }
	public ModuleDefinition Definition { get; }
	public TypeDefinition GlobalFunctionsType { get; }
	public TypeDefinition PointerCacheType { get; }
	public TypeDefinition GlobalVariablePointersType { get; }
	public TypeDefinition GlobalVariablesType { get; }
	public TypeDefinition IntrinsicsType { get; }
	public TypeDefinition InlineArrayHelperType { get; }
	public TypeDefinition PrivateImplementationDetails { get; }
	private IMethodDefOrRef CompilerGeneratedAttributeConstructor { get; }
	public Dictionary<LLVMValueRef, FunctionContext> Methods { get; } = new();
	public Dictionary<string, TypeDefinition> Structs { get; } = new();
	public Dictionary<LLVMValueRef, GlobalVariableContext> GlobalVariables { get; } = new();
	private readonly Dictionary<(TypeSignature, int), TypeDefinition> inlineArrayCache = new(TypeSignatureIntPairComparer);
	public Dictionary<TypeDefinition, (TypeSignature, int)> InlineArrayTypes { get; } = new(SignatureComparer.Default);

	private static PairEqualityComparer<TypeSignature, int> TypeSignatureIntPairComparer { get; } = new(SignatureComparer.Default, EqualityComparer<int>.Default);

	public TypeDefinition GetOrCreateInlineArray(TypeSignature type, int size)
	{
		(TypeSignature, int) pair = (type, size);
		if (!inlineArrayCache.TryGetValue(pair, out TypeDefinition? arrayType))
		{
			string name = $"InlineArray_{size}";
			string uniqueName = NameGenerator.GenerateName(name, type.FullName);

			arrayType = new TypeDefinition("InlineArrays", uniqueName, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed, Definition.DefaultImporter.ImportType(typeof(ValueType)));
			Definition.TopLevelTypes.Add(arrayType);

			//Add InlineArrayAttribute to arrayType
			{
				System.Reflection.ConstructorInfo constructorInfo = typeof(InlineArrayAttribute).GetConstructors().Single();
				IMethodDescriptor inlineArrayAttributeConstructor = Definition.DefaultImporter.ImportMethod(constructorInfo);
				CustomAttributeArgument argument = new(Definition.CorLibTypeFactory.Int32, size);
				CustomAttributeSignature signature = new(argument);
				arrayType.CustomAttributes.Add(new CustomAttribute((ICustomAttributeType)inlineArrayAttributeConstructor, signature));
			}

			//Add private instance field with the cooresponding type.
			{
				FieldDefinition field = new("__element0", FieldAttributes.Private, type);
				arrayType.Fields.Add(field);
			}

			inlineArrayCache.Add(pair, arrayType);
			InlineArrayTypes.Add(arrayType, pair);
		}

		return arrayType;
	}

	public void CreateFunctions()
	{
		foreach (LLVMValueRef function in Module.GetFunctions())
		{
			MethodDefinition method = CreateNewMethod(GlobalFunctionsType);
			FunctionContext functionContext = FunctionContext.Create(function, method, this);
			Methods.Add(function, functionContext);
		}
	}

	public void AssignFunctionNames() => AssignNames(Methods.Values);

	public void AssignGlobalVariableNames() => AssignNames(GlobalVariables.Values);

	private static void AssignNames<T>(IEnumerable<T> items) where T : IHasName
	{
		Dictionary<string, List<T>> demangledNames = new();
		foreach (T item in items)
		{
			if (!demangledNames.TryGetValue(item.CleanName, out List<T>? list))
			{
				list = new();
				demangledNames.Add(item.CleanName, list);
			}
			list.Add(item);
		}

		foreach ((string cleanName, List<T> list) in demangledNames)
		{
			if (list.Count == 1)
			{
				list[0].Name = cleanName;
			}
			else
			{
				foreach (T item in list)
				{
					item.Name = NameGenerator.GenerateName(cleanName, item.MangledName);
				}
			}
		}
	}

	public void AssignStructNames()
	{
		Dictionary<string, List<(string UniqueName, TypeDefinition Type)>> cleanNames = new();
		foreach ((string name, TypeDefinition type) in Structs)
		{
			string cleanName = ExtractCleanName(name);
			if (!cleanNames.TryGetValue(cleanName, out List<(string UniqueName, TypeDefinition Type)>? list))
			{
				list = new();
				cleanNames.Add(cleanName, list);
			}
			list.Add((name, type));
		}

		foreach ((string cleanName, List<(string UniqueName, TypeDefinition Type)> list) in cleanNames)
		{
			if (list.Count == 1)
			{
				list[0].Type.Name = cleanName;
			}
			else
			{
				foreach ((string uniqueName, TypeDefinition type) in list)
				{
					type.Name = NameGenerator.GenerateName(cleanName, uniqueName);
				}
			}
		}

		static string ExtractCleanName(string name)
		{
			const string StructPrefix = "struct.";
			if (name.StartsWith(StructPrefix, StringComparison.Ordinal))
			{
				name = name[StructPrefix.Length..];
			}
			return name.Length > 0 ? name : "Struct";
		}
	}

	public void InitializeMethodSignatures()
	{
		foreach (FunctionContext functionContext in Methods.Values)
		{
			MethodDefinition method = functionContext.Definition;
			Debug.Assert(method.Signature is not null);

			method.Name = functionContext.Name;

			TypeSignature returnTypeSignature = GetTypeSignature(functionContext.ReturnType);

			method.Signature.ReturnType = returnTypeSignature;

			for (int i = 0; i < functionContext.Parameters.Length; i++)
			{
				LLVMValueRef parameter = functionContext.Parameters[i];
				TypeSignature parameterType;
				if (i == 0 && functionContext.TryGetStructReturnType(out LLVMTypeRef type))
				{
					parameterType = GetTypeSignature(type).MakePointerType();
				}
				else
				{
					parameterType = GetTypeSignature(parameter.TypeOf);
				}
				functionContext.ParameterDictionary[parameter] = method.AddParameter(parameterType);
			}

			if (functionContext.IsVariadic)
			{
				TypeSignature pointerSpan = Definition.DefaultImporter
					.ImportType(typeof(ReadOnlySpan<>))
					.MakeGenericInstanceType(Definition.CorLibTypeFactory.IntPtr);
				TypeSignature typeSpan = Definition.DefaultImporter
					.ImportType(typeof(ReadOnlySpan<>))
					.MakeGenericInstanceType(Definition.DefaultImporter.ImportType(typeof(Type)).ToTypeSignature());

				method.AddParameter(pointerSpan).GetOrCreateDefinition().Name = "args";
				method.AddParameter(typeSpan).GetOrCreateDefinition().Name = "types";
			}
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
					//128
					_ => throw new NotSupportedException(),
				};

			case LLVMTypeKind.LLVMFunctionTypeKind:
				//I don't think this can happen, but void* is fine.
				return Definition.CorLibTypeFactory.Void.MakePointerType();

			case LLVMTypeKind.LLVMStructTypeKind:
				{
					string name = type.StructName;
					if (!Structs.TryGetValue(name, out TypeDefinition? typeDefinition))
					{
						typeDefinition = new(
							"Structures",
							name,
							TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit,
							Definition.DefaultImporter.ImportType(typeof(ValueType)));
						Definition.TopLevelTypes.Add(typeDefinition);
						Structs.Add(name, typeDefinition);

						LLVMTypeRef[] array = type.GetSubtypes();
						for (int i = 0; i < array.Length; i++)
						{
							LLVMTypeRef subType = array[i];
							TypeSignature fieldType = GetTypeSignature(subType);
							string fieldName = $"field_{i}";
							FieldDefinition field = new(fieldName, FieldAttributes.Public, fieldType);
							typeDefinition.Fields.Add(field);
						}
					}
					return typeDefinition.ToTypeSignature();
				}

			case LLVMTypeKind.LLVMArrayTypeKind:
				{
					TypeSignature elementType = GetTypeSignature(type.ElementType);
					int count = (int)type.ArrayLength;
					TypeDefinition arrayType = GetOrCreateInlineArray(elementType, count);
					return arrayType.ToTypeSignature();
				}

			case LLVMTypeKind.LLVMPointerTypeKind:
				//All pointers are opaque in IR
				return Definition.CorLibTypeFactory.Void.MakePointerType();

			case LLVMTypeKind.LLVMVectorTypeKind:
				goto default;

			case LLVMTypeKind.LLVMMetadataTypeKind:
				goto default;

			case LLVMTypeKind.LLVMX86_MMXTypeKind:
				goto default;

			case LLVMTypeKind.LLVMTokenTypeKind:
				return Definition.CorLibTypeFactory.Void;

			case LLVMTypeKind.LLVMScalableVectorTypeKind:
				goto default;

			case LLVMTypeKind.LLVMBFloatTypeKind:
				//Half is just an approximation of BFloat16, which is not yet supported in .NET
				//Maybe we can use this instead: https://www.nuget.org/packages/UltimateOrb.TruncatedFloatingPoints
				return Definition.DefaultImporter.ImportTypeSignature(typeof(Half));

			case LLVMTypeKind.LLVMX86_AMXTypeKind:
				goto default;

			case LLVMTypeKind.LLVMTargetExtTypeKind:
				goto default;

			default:
				throw new NotSupportedException();
		}
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
					long integer = value.ConstIntSExt;
					LLVMTypeRef operandType = value.TypeOf;
					if (integer is <= int.MaxValue and >= int.MinValue && operandType is { IntWidth: <= sizeof(int) * 8 })
					{
						instructions.Add(CilOpCodes.Ldc_I4, (int)integer);
					}
					else
					{
						instructions.Add(CilOpCodes.Ldc_I8, integer);
					}
					typeSignature = GetTypeSignature(operandType);
				}
				break;
			case LLVMValueKind.LLVMGlobalVariableValueKind:
				{
					GlobalVariableContext global = GlobalVariables[value];

					MethodDefinition pointerGetMethod = global.PointerGetMethod;

					instructions.Add(CilOpCodes.Call, pointerGetMethod);

					typeSignature = pointerGetMethod.Signature!.ReturnType;
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
					(TypeSignature elementType, int elementCount) = InlineArrayTypes[inlineArrayType];

					ReadOnlySpan<byte> data = LibLLVMSharp.ConstantDataArrayGetData(value);

					CilLocalVariable spanLocal = instructions.AddLocalVariable(null!);

					if (elementType is CorLibTypeSignature { ElementType: ElementType.I2 } && TryParseCharacterArray(data, out string? @string))
					{
						elementType = Definition.CorLibTypeFactory.Char;

						IMethodDescriptor toCharacterSpan = InlineArrayHelperType.Methods
							.Single(m => m.Name == nameof(InlineArrayHelper.ToCharacterSpan));

						instructions.Add(CilOpCodes.Ldstr, @string);
						instructions.Add(CilOpCodes.Call, toCharacterSpan);
						instructions.Add(CilOpCodes.Stloc, spanLocal);
					}
					else if (elementType is CorLibTypeSignature { ElementType: ElementType.I1 or ElementType.U1 })
					{
						elementType = Definition.CorLibTypeFactory.Byte;

						IMethodDefOrRef spanConstructor = (IMethodDefOrRef)Definition.DefaultImporter
							.ImportMethod(typeof(ReadOnlySpan<byte>).GetConstructor([typeof(void*), typeof(int)])!);

						FieldDefinition field = AddStoredDataField(data.ToArray());
						instructions.Add(CilOpCodes.Ldloca, spanLocal);
						instructions.Add(CilOpCodes.Ldsflda, field);
						instructions.Add(CilOpCodes.Ldc_I4, data.Length);
						instructions.Add(CilOpCodes.Call, spanConstructor);
					}
					else
					{
						IMethodDefOrRef createSpan = (IMethodDefOrRef)Definition.DefaultImporter
							.ImportMethod(typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateSpan))!);
						IMethodDescriptor createSpanInstance = createSpan.MakeGenericInstanceMethod(elementType);

						FieldDefinition field = AddStoredDataField(data.ToArray());
						instructions.Add(CilOpCodes.Ldtoken, field);
						instructions.Add(CilOpCodes.Call, createSpanInstance);
						instructions.Add(CilOpCodes.Stloc, spanLocal);
					}

					ITypeDescriptor spanType = Definition.DefaultImporter
						.ImportType(typeof(ReadOnlySpan<>))
						.MakeGenericInstanceType(elementType);

					IMethodDescriptor createInlineArray = InlineArrayHelperType.Methods
						.Single(m => m.Name == nameof(InlineArrayHelper.Create))
						.MakeGenericInstanceMethod(typeSignature, elementType);

					spanLocal.VariableType = spanType.ToTypeSignature();

					instructions.Add(CilOpCodes.Ldloc, spanLocal);
					instructions.Add(CilOpCodes.Call, createInlineArray);
				}
				break;
			case LLVMValueKind.LLVMConstantArrayValueKind:
				{
					TypeSignature underlyingType = GetTypeSignature(value.TypeOf);

					LLVMValueRef[] elements = value.GetOperands();

					(TypeSignature elementType, int elementCount) = InlineArrayTypes[(TypeDefinition)underlyingType.ToTypeDefOrRef()];

					if (elementCount != elements.Length)
					{
						throw new Exception("Array element count mismatch");
					}

					if (elementType is PointerTypeSignature)
					{
						elementType = Definition.CorLibTypeFactory.IntPtr;
					}

					TypeSignature spanType = Definition.DefaultImporter
						.ImportType(typeof(Span<>))
						.MakeGenericInstanceType(elementType);

					IMethodDescriptor inlineArrayAsSpan = InlineArrayHelperType.Methods
						.Single(m => m.Name == nameof(InlineArrayHelper.InlineArrayAsSpan))
						.MakeGenericInstanceMethod(underlyingType, elementType);

					MethodSignature getItemSignature = MethodSignature.CreateInstance(new GenericParameterSignature(GenericParameterType.Type, 0).MakeByReferenceType(), Definition.CorLibTypeFactory.Int32);
					IMethodDescriptor getItem = new MemberReference(spanType.ToTypeDefOrRef(), "get_Item", getItemSignature);

					CilLocalVariable bufferLocal = instructions.AddLocalVariable(underlyingType);
					CilLocalVariable spanLocal = instructions.AddLocalVariable(spanType);

					instructions.AddDefaultValue(underlyingType);
					instructions.Add(CilOpCodes.Stloc, bufferLocal);

					instructions.Add(CilOpCodes.Ldloca, bufferLocal);
					instructions.Add(CilOpCodes.Ldc_I4, elementCount);
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
				{
					typeSignature = GetTypeSignature(value.TypeOf);
					instructions.AddDefaultValue(typeSignature);
				}
				break;
			case LLVMValueKind.LLVMFunctionValueKind:
				{
					typeSignature = GetTypeSignature(value.TypeOf);
					MethodDefinition method = Methods[value].Definition;

					instructions.Add(CilOpCodes.Ldftn, method);
				}
				break;
			case LLVMValueKind.LLVMConstantExprValueKind:
				{
					typeSignature = GetTypeSignature(value.TypeOf);
					InstructionContext expression = InstructionContext.Create(value, this);
					expression.CreateLocal(instructions);
					expression.AddInstructions(instructions);
					instructions.Add(CilOpCodes.Ldloc, expression.GetLocalVariable());
				}
				break;
			case LLVMValueKind.LLVMInstructionValueKind:
			case LLVMValueKind.LLVMArgumentValueKind:
				throw new NotSupportedException();
			default:
				throw new NotImplementedException();
		}
	}

	private static MethodDefinition CreateNewMethod(TypeDefinition globalMembersType)
	{
		MethodSignature signature = MethodSignature.CreateStatic(null!);
		MethodDefinition method = new(null, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, signature);
		method.CilMethodBody = new(method);
		globalMembersType.Methods.Add(method);
		return method;
	}

	private static TypeDefinition CreateStaticType(ModuleDefinition moduleDefinition, string name, bool @public = true)
	{
		TypeDefinition typeDefinition = new(null, name, (@public ? TypeAttributes.Public : TypeAttributes.NotPublic) | TypeAttributes.Abstract | TypeAttributes.Sealed);
		moduleDefinition.TopLevelTypes.Add(typeDefinition);
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
		FieldDefinition privateImplementationField = new FieldDefinition(fieldName, FieldAttributes.Assembly | FieldAttributes.Static, fieldType);
		privateImplementationField.IsInitOnly = true;
		privateImplementationField.FieldRva = new DataSegment(data);
		privateImplementationField.HasFieldRva = true;
		AddCompilerGeneratedAttribute(privateImplementationField);

		PrivateImplementationDetails.Fields.Add(privateImplementationField);

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

	private static bool TryParseCharacterArray(ReadOnlySpan<byte> data, [NotNullWhen(true)] out string? value)
	{
		if (data.Length % sizeof(char) != 0)
		{
			value = null;
			return false;
		}
		
		char[] chars = new char[data.Length / sizeof(char)];
		for (int i = 0; i < chars.Length; i++)
		{
			char c = (char)BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(i * sizeof(char)));
			if (!char.IsAscii(c))
			{
				value = null;
				return false;
			}
			if (char.IsControl(c) && (i != chars.Length - 1 || c != '\0')) // Allow null terminator
			{
				value = null;
				return false;
			}
			chars[i] = c;
		}
		value = new string(chars);
		return true;
	}
}
