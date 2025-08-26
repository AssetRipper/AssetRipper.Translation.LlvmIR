using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using System.Numerics;
using System.Runtime.CompilerServices;
using AsmResolverElementType = AsmResolver.PE.DotNet.Metadata.Tables.ElementType;

namespace AssetRipper.Translation.LlvmIR;

internal sealed class InlineArrayContext
{
	public ModuleContext Module { get; }
	public TypeDefinition Type { get; }
	public TypeSignature ElementType { get; }
	public int Length { get; }
	public TypeSignature UltimateElementType
	{
		get
		{
			GetUltimateElementType(out TypeSignature elementType, out _);
			return elementType;
		}
	}

	private InlineArrayContext(ModuleContext module, TypeDefinition type, TypeSignature elementType, int length)
	{
		Module = module;
		Type = type;
		ElementType = elementType;
		Length = length;
	}

	public void GetElementType(out TypeSignature elementType, out int length)
	{
		elementType = ElementType;
		length = Length;
	}

	/// <summary>
	/// Determines the ultimate element type of the inline array, iterating through any nested inline arrays.
	/// </summary>
	/// <param name="elementType">The ultimate element type.</param>
	/// <param name="length">The ultimate length.</param>
	/// <returns>True if any nested inline arrays were encountered.</returns>
	public bool GetUltimateElementType(out TypeSignature elementType, out int length)
	{
		elementType = ElementType;
		length = Length;

		while (elementType.Resolve() is { } type && Module.InlineArrayTypes.TryGetValue(type, out InlineArrayContext? inlineArray))
		{
			elementType = inlineArray.ElementType;
			length *= inlineArray.Length;
		}

		return !ReferenceEquals(elementType, ElementType);
	}

	public static InlineArrayContext CreateInlineArray(TypeSignature type, int size, ModuleContext module)
	{
		string name = $"InlineArray_{size}";
		string uniqueName = NameGenerator.GenerateName(name, type.FullName);

		TypeDefinition arrayType = new(module.Options.GetNamespace("InlineArrays"), uniqueName, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed, module.Definition.DefaultImporter.ImportType(typeof(ValueType)));
		module.Definition.TopLevelTypes.Add(arrayType);

		//Add InlineArrayAttribute to arrayType
		{
			System.Reflection.ConstructorInfo constructorInfo = typeof(InlineArrayAttribute).GetConstructors().Single();
			IMethodDescriptor inlineArrayAttributeConstructor = module.Definition.DefaultImporter.ImportMethod(constructorInfo);
			CustomAttributeArgument argument = new(module.Definition.CorLibTypeFactory.Int32, size);
			CustomAttributeSignature signature = new(argument);
			arrayType.CustomAttributes.Add(new CustomAttribute((ICustomAttributeType)inlineArrayAttributeConstructor, signature));
		}

		//Add private instance field with the cooresponding type.
		{
			FieldDefinition field = new("__element0", FieldAttributes.Private, type);
			arrayType.Fields.Add(field);
		}

		//Equality operator
		MethodDefinition equalityOperator;
		{
			equalityOperator = new("op_Equality", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static, MethodSignature.CreateStatic(module.Definition.CorLibTypeFactory.Boolean, arrayType.ToTypeSignature(), arrayType.ToTypeSignature()));
			equalityOperator.CilMethodBody = new(equalityOperator);
			CilInstructionCollection instructions = equalityOperator.CilMethodBody.Instructions;
			MethodDefinition helperMethod = module.InlineArrayHelperType.GetMethodByName(nameof(InlineArrayHelper.Equals));
			instructions.Add(CilOpCodes.Ldarg_0);
			instructions.Add(CilOpCodes.Ldarg_1);
			instructions.Add(CilOpCodes.Call, helperMethod.MakeGenericInstanceMethod(arrayType.ToTypeSignature(), type));
			instructions.Add(CilOpCodes.Ret);
			arrayType.Methods.Add(equalityOperator);

			equalityOperator.Parameters[0].GetOrCreateDefinition().Name = "x";
			equalityOperator.Parameters[1].GetOrCreateDefinition().Name = "y";
		}

		//Inequality operator
		MethodDefinition inequalityOperator;
		{
			inequalityOperator = new("op_Inequality", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static, MethodSignature.CreateStatic(module.Definition.CorLibTypeFactory.Boolean, arrayType.ToTypeSignature(), arrayType.ToTypeSignature()));
			inequalityOperator.CilMethodBody = new(inequalityOperator);
			CilInstructionCollection instructions = inequalityOperator.CilMethodBody.Instructions;
			instructions.Add(CilOpCodes.Ldarg_0);
			instructions.Add(CilOpCodes.Ldarg_1);
			instructions.Add(CilOpCodes.Call, equalityOperator);
			instructions.AddBooleanNot();
			instructions.Add(CilOpCodes.Ret);
			arrayType.Methods.Add(inequalityOperator);

			inequalityOperator.Parameters[0].GetOrCreateDefinition().Name = "x";
			inequalityOperator.Parameters[1].GetOrCreateDefinition().Name = "y";
		}

		//Equals(InlineArray)
		MethodDefinition equalsMethod;
		{
			equalsMethod = new("Equals", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot, MethodSignature.CreateInstance(module.Definition.CorLibTypeFactory.Boolean, arrayType.ToTypeSignature()));
			equalsMethod.CilMethodBody = new(equalsMethod);
			CilInstructionCollection instructions = equalsMethod.CilMethodBody.Instructions;
			instructions.Add(CilOpCodes.Ldarg_0);
			instructions.Add(CilOpCodes.Ldobj, arrayType);
			instructions.Add(CilOpCodes.Ldarg_1);
			instructions.Add(CilOpCodes.Call, equalityOperator);
			instructions.Add(CilOpCodes.Ret);
			arrayType.Methods.Add(equalsMethod);

			equalsMethod.Parameters[0].GetOrCreateDefinition().Name = "other";
		}

		//Equals(object)
		{
			// return other is InlineArray array && Equals(array);
			MethodDefinition method = new("Equals", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual, MethodSignature.CreateInstance(module.Definition.CorLibTypeFactory.Boolean, module.Definition.CorLibTypeFactory.Object));
			method.CilMethodBody = new(method);
			CilInstructionCollection instructions = method.CilMethodBody.Instructions;
			CilInstructionLabel falseLabel = new();
			instructions.Add(CilOpCodes.Ldarg_1);
			instructions.Add(CilOpCodes.Isinst, arrayType);
			instructions.Add(CilOpCodes.Brfalse_S, falseLabel);
			instructions.Add(CilOpCodes.Ldarg_0);
			instructions.Add(CilOpCodes.Ldarg_1);
			instructions.Add(CilOpCodes.Unbox_Any, arrayType);
			instructions.Add(CilOpCodes.Call, equalsMethod);
			instructions.Add(CilOpCodes.Ret);
			falseLabel.Instruction = instructions.Add(CilOpCodes.Ldc_I4_0);
			instructions.Add(CilOpCodes.Ret);
			arrayType.Methods.Add(method);

			method.Parameters[0].GetOrCreateDefinition().Name = "other";
		}

		//GetHashCode
		{
			MethodDefinition method = new("GetHashCode", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual, MethodSignature.CreateInstance(module.Definition.CorLibTypeFactory.Int32));
			method.CilMethodBody = new(method);
			CilInstructionCollection instructions = method.CilMethodBody.Instructions;
			MethodDefinition helperMethod = module.InlineArrayHelperType.GetMethodByName(nameof(InlineArrayHelper.GetHashCode));
			instructions.Add(CilOpCodes.Ldarg_0);
			instructions.Add(CilOpCodes.Call, helperMethod.MakeGenericInstanceMethod(arrayType.ToTypeSignature(), type));
			instructions.Add(CilOpCodes.Ret);
			arrayType.Methods.Add(method);
		}

		//IEquatable<> interface
		{
			ITypeDefOrRef iequatable = module.Definition.DefaultImporter.ImportType(typeof(IEquatable<>));
			GenericInstanceTypeSignature genericInstance = iequatable.MakeGenericInstanceType(arrayType.ToTypeSignature());
			arrayType.Interfaces.Add(new InterfaceImplementation(genericInstance.ToTypeDefOrRef()));
		}

		//IEqualityOperators
		{
			ITypeDefOrRef iEqualityOperators = module.Definition.DefaultImporter.ImportType(typeof(IEqualityOperators<,,>));
			ITypeDefOrRef genericInstance = iEqualityOperators.MakeGenericInstanceType(arrayType.ToTypeSignature(), arrayType.ToTypeSignature(), module.Definition.CorLibTypeFactory.Boolean).ToTypeDefOrRef();
			arrayType.Interfaces.Add(new InterfaceImplementation(genericInstance));

			MethodSignature methodSignature = MethodSignature.CreateStatic(new GenericParameterSignature(GenericParameterType.Type, 2), new GenericParameterSignature(GenericParameterType.Type, 0), new GenericParameterSignature(GenericParameterType.Type, 1));

			// op_Equality
			{
				MemberReference interfaceMethod = new(genericInstance, "op_Equality", methodSignature);
				arrayType.MethodImplementations.Add(new MethodImplementation(interfaceMethod, equalityOperator));
			}

			// op_Inequality
			{
				MemberReference interfaceMethod = new(genericInstance, "op_Inequality", methodSignature);
				arrayType.MethodImplementations.Add(new MethodImplementation(interfaceMethod, inequalityOperator));
			}
		}

		InlineArrayContext result = new(module, arrayType, type, size);

		result.ImplementInterface();

		return result;
	}

	private void ImplementInterface()
	{
		// Normal implementation
		{
			ImplementInterfaceForType(ElementType, Length, false);
		}

		// Special implementation
		if (GetUltimateElementType(out TypeSignature elementType, out int length))
		{
			ImplementInterfaceForType(elementType, length, true);
		}

		if (elementType.TryGetReverseSign(out TypeSignature? opposite))
		{
			ImplementInterfaceForType(opposite, length, true);
		}

		if (elementType is CorLibTypeSignature { ElementType: AsmResolverElementType.I2 or AsmResolverElementType.U2 })
		{
			ImplementInterfaceForType(Module.Definition.CorLibTypeFactory.Char, length, true);
		}

		void ImplementInterfaceForType(TypeSignature elementType, int length, bool @explicit)
		{
			const string propertyName = nameof(IInlineArray<>.Length);
			const string methodName = $"get_{propertyName}";
			MethodSignature methodSignature = MethodSignature.CreateStatic(Module.Definition.CorLibTypeFactory.Int32);

			GenericInstanceTypeSignature interfaceType1 = Module.InjectedTypes[typeof(IInlineArray<>)].MakeGenericInstanceType(elementType);
			GenericInstanceTypeSignature interfaceType2 = Module.InjectedTypes[typeof(IInlineArray<,>)].MakeGenericInstanceType(Type.ToTypeSignature(), elementType);

			Type.Interfaces.Add(new InterfaceImplementation(interfaceType2.ToTypeDefOrRef()));

			MemberReference interfaceMethod = new(interfaceType1.ToTypeDefOrRef(), methodName, methodSignature);

			if (@explicit && Length == length)
			{
				Type.MethodImplementations.Add(new MethodImplementation(interfaceMethod, Type.GetMethodByName(methodName)));
				return;
			}

			string prefix;
			MethodAttributes attributes;
			if (@explicit)
			{
				string @namespace = interfaceType1.Namespace is { Length: > 0 }
					? interfaceType1.Namespace + "."
					: string.Empty;
				prefix = $"{@namespace}{nameof(IInlineArray<>)}<{elementType.FullName}>.";
				attributes = MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static;
			}
			else
			{
				prefix = "";
				attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static;
			}

			MethodDefinition method = new(prefix + methodName, attributes, methodSignature);
			method.CilMethodBody = new(method);

			CilInstructionCollection instructions = method.CilMethodBody.Instructions;
			instructions.Add(CilOpCodes.Ldc_I4, length);
			instructions.Add(CilOpCodes.Ret);
			instructions.OptimizeMacros();

			Type.Methods.Add(method);
			Type.MethodImplementations.Add(new MethodImplementation(interfaceMethod, method));

			PropertyDefinition property = new(prefix + propertyName, PropertyAttributes.None, PropertySignature.CreateStatic(Module.Definition.CorLibTypeFactory.Int32));
			property.GetMethod = method;
			Type.Properties.Add(property);
		}
	}
}
