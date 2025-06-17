using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.Translation.LlvmIR.Extensions;
using System.Runtime.CompilerServices;

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

		TypeDefinition arrayType = new("InlineArrays", uniqueName, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed, module.Definition.DefaultImporter.ImportType(typeof(ValueType)));
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

		InlineArrayContext result = new(module, arrayType, type, size);

		result.ImplementInterface();

		return result;
	}

	private void ImplementInterface()
	{
		string propertyName = nameof(IInlineArray<>.Length);
		string methodName = $"get_{propertyName}";
		MethodSignature methodSignature = MethodSignature.CreateStatic(Module.Definition.CorLibTypeFactory.Int32);
		PropertySignature propertySignature = PropertySignature.CreateStatic(Module.Definition.CorLibTypeFactory.Int32);

		// Normal implementation
		{
			GenericInstanceTypeSignature interfaceType = Module.InlineArrayInterface.MakeGenericInstanceType(ElementType);

			Type.Interfaces.Add(new InterfaceImplementation(interfaceType.ToTypeDefOrRef()));

			MemberReference interfaceMethod = new(interfaceType.ToTypeDefOrRef(), methodName, methodSignature);

			MethodDefinition method = new(methodName, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static, methodSignature);
			method.CilMethodBody = new(method);

			CilInstructionCollection instructions = method.CilMethodBody.Instructions;
			instructions.Add(CilOpCodes.Ldc_I4, Length);
			instructions.Add(CilOpCodes.Ret);
			instructions.OptimizeMacros();

			Type.Methods.Add(method);
			Type.MethodImplementations.Add(new MethodImplementation(interfaceMethod, method));

			PropertyDefinition property = new(propertyName, PropertyAttributes.None, propertySignature);
			property.GetMethod = method;
			Type.Properties.Add(property);
		}

		// Special implementation
		if (GetUltimateElementType(out TypeSignature elementType, out int length))
		{
			GenericInstanceTypeSignature interfaceType = Module.InlineArrayInterface.MakeGenericInstanceType(elementType);

			Type.Interfaces.Add(new InterfaceImplementation(interfaceType.ToTypeDefOrRef()));

			MemberReference interfaceMethod = new(interfaceType.ToTypeDefOrRef(), methodName, methodSignature);

			string @namespace = interfaceType.Namespace is { Length: > 0 }
				? interfaceType.Namespace + "."
				: string.Empty;
			string prefix = $"{@namespace}{nameof(IInlineArray<>)}<{elementType.FullName}>.";

			MethodDefinition method = new(prefix + methodName, MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static, methodSignature);
			method.CilMethodBody = new(method);

			CilInstructionCollection instructions = method.CilMethodBody.Instructions;
			instructions.Add(CilOpCodes.Ldc_I4, length);
			instructions.Add(CilOpCodes.Ret);
			instructions.OptimizeMacros();

			Type.Methods.Add(method);
			Type.MethodImplementations.Add(new MethodImplementation(interfaceMethod, method));

			PropertyDefinition property = new(prefix + propertyName, PropertyAttributes.None, propertySignature);
			property.GetMethod = method;
			Type.Properties.Add(property);
		}

		if (elementType.TryGetReverseSign(out TypeSignature? opposite))
		{
			GenericInstanceTypeSignature interfaceType = Module.InlineArrayInterface.MakeGenericInstanceType(opposite);

			Type.Interfaces.Add(new InterfaceImplementation(interfaceType.ToTypeDefOrRef()));

			MemberReference interfaceMethod = new(interfaceType.ToTypeDefOrRef(), methodName, methodSignature);

			string @namespace = interfaceType.Namespace is { Length: > 0 }
				? interfaceType.Namespace + "."
				: string.Empty;
			string prefix = $"{@namespace}{nameof(IInlineArray<>)}<{opposite.FullName}>.";

			MethodDefinition method = new(prefix + methodName, MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static, methodSignature);
			method.CilMethodBody = new(method);

			CilInstructionCollection instructions = method.CilMethodBody.Instructions;
			instructions.Add(CilOpCodes.Ldc_I4, length);
			instructions.Add(CilOpCodes.Ret);
			instructions.OptimizeMacros();

			Type.Methods.Add(method);
			Type.MethodImplementations.Add(new MethodImplementation(interfaceMethod, method));

			PropertyDefinition property = new(prefix + propertyName, PropertyAttributes.None, propertySignature);
			property.GetMethod = method;
			Type.Properties.Add(property);
		}
	}
}
