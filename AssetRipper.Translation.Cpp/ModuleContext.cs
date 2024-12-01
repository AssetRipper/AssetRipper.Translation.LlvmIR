using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
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
		GlobalMembersType = CreateGlobalMembersType(definition);
		ConstantsType = CreateConstantsType(definition);
		IntrinsicsType = IntrinsicFunctionImplementer.InjectIntrinsics(Definition);

		CompilerGeneratedAttributeConstructor = (IMethodDefOrRef)definition.DefaultImporter.ImportMethod(typeof(CompilerGeneratedAttribute).GetConstructors()[0]);

		PrivateImplementationDetails = new TypeDefinition(null, "<PrivateImplementationDetails>", TypeAttributes.NotPublic | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed);
		AddCompilerGeneratedAttribute(PrivateImplementationDetails);
		Definition.TopLevelTypes.Add(PrivateImplementationDetails);
	}

	public LLVMModuleRef Module { get; }
	public ModuleDefinition Definition { get; }
	public TypeDefinition GlobalMembersType { get; }
	public TypeDefinition ConstantsType { get; }
	public TypeDefinition IntrinsicsType { get; }
	public TypeDefinition PrivateImplementationDetails { get; }
	private IMethodDefOrRef CompilerGeneratedAttributeConstructor { get; }
	public Dictionary<LLVMValueRef, FunctionContext> Methods { get; } = new();
	public Dictionary<string, TypeDefinition> Structs { get; } = new();
	public Dictionary<LLVMValueRef, FieldDefinition> GlobalConstants { get; } = new();
	private readonly Dictionary<(TypeSignature, int), TypeDefinition> inlineArrayCache = new();

	public TypeDefinition GetOrCreateInlineArray(TypeSignature type, int size)
	{
		(TypeSignature, int) pair = (type, size);
		if (!inlineArrayCache.TryGetValue(pair, out TypeDefinition? arrayType))
		{
			string name = $"InlineArray_{inlineArrayCache.Count}";//Could be better, but it's unique, so it's good enough for now.
			arrayType = new TypeDefinition(null, name, default(TypeAttributes));
			Definition.TopLevelTypes.Add(arrayType);

			//Add InlineArrayAttribute to arrayType
			//Add private instance field with the cooresponding type.
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

	public void AssignFunctionNames()
	{
		Dictionary<string, List<FunctionContext>> demangledNames = new();
		foreach ((LLVMValueRef function, FunctionContext functionContext) in Methods)
		{
			functionContext.DemangledName = LibLLVMSharp.ValueGetDemangledName(function);
			functionContext.CleanName = ExtractCleanName(functionContext.MangledName).Replace('.', '_');

			if (!demangledNames.TryGetValue(functionContext.CleanName, out List<FunctionContext>? list))
			{
				list = new();
				demangledNames.Add(functionContext.CleanName, list);
			}
			list.Add(functionContext);
		}

		foreach ((string cleanName, List<FunctionContext> list) in demangledNames)
		{
			if (list.Count == 1)
			{
				list[0].Name = cleanName;
			}
			else
			{
				foreach (FunctionContext functionContext in list)
				{
					functionContext.Name = NameGenerator.GenerateName(cleanName, functionContext.MangledName);
				}
			}
		}
	}

	public void InitializeMethodSignatures()
	{
		foreach ((LLVMValueRef function, FunctionContext functionContext) in Methods)
		{
			MethodDefinition method = functionContext.Definition;
			Debug.Assert(method.Signature is not null);

			method.Name = functionContext.Name;

			TypeSignature returnTypeSignature = GetTypeSignature(functionContext.ReturnType);

			method.Signature.ReturnType = returnTypeSignature;

			foreach (LLVMValueRef parameter in function.GetParams())
			{
				LLVMTypeRef type = parameter.TypeOf;
				TypeSignature parameterType = GetTypeSignature(type);
				functionContext.ParameterDictionary[parameter] = method.AddParameter(parameterType);
			}
		}
	}

	public TypeSignature? GetTypeSignature(LLVMTypeRef type)
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
							null,
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
				goto default;
			case LLVMTypeKind.LLVMPointerTypeKind:
				//All pointers are opaque in IR
				return null;
			case LLVMTypeKind.LLVMVectorTypeKind:
				goto default;
			case LLVMTypeKind.LLVMMetadataTypeKind:
				goto default;
			case LLVMTypeKind.LLVMX86_MMXTypeKind:
				goto default;
			case LLVMTypeKind.LLVMTokenTypeKind:
				goto default;
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

	private static TypeDefinition CreateGlobalMembersType(ModuleDefinition moduleDefinition)
	{
		return CreateStaticType(moduleDefinition, "GlobalMembers");
	}

	private static TypeDefinition CreateConstantsType(ModuleDefinition moduleDefinition)
	{
		return CreateStaticType(moduleDefinition, "Constants");
	}

	private static TypeDefinition CreateStaticType(ModuleDefinition moduleDefinition, string name)
	{
		TypeDefinition typeDefinition = new(null, name, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
		moduleDefinition.TopLevelTypes.Add(typeDefinition);
		return typeDefinition;
	}

	private static string ExtractCleanName(string name)
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
}
