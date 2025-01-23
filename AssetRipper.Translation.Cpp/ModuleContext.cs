using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace AssetRipper.Translation.Cpp;

internal sealed class ModuleContext
{
	public ModuleContext(LLVMModuleRef module, ModuleDefinition definition)
	{
		Module = module;
		Definition = definition;
		GlobalMembersType = CreateStaticType(definition, "GlobalMembers");
		ConstantsType = CreateStaticType(definition, "Constants");
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
	public TypeDefinition GlobalMembersType { get; }
	public TypeDefinition ConstantsType { get; }
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
			string name = $"InlineArray_{inlineArrayCache.Count}";//Could be better, but it's unique, so it's good enough for now.

			arrayType = new TypeDefinition("InlineArrays", name, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed, Definition.DefaultImporter.ImportType(typeof(ValueType)));
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
			MethodDefinition method = CreateNewMethod(GlobalMembersType);
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
				return name[StructPrefix.Length..];
			}
			else
			{
				return name;
			}
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
}
