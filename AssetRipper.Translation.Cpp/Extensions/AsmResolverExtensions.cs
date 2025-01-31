using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;

namespace AssetRipper.Translation.Cpp.Extensions;

internal static class AsmResolverExtensions
{
	public static void AddTargetFrameworkAttributeForDotNet9(this ModuleDefinition module)
	{
		IMethodDescriptor constructor = module.DefaultImporter.ImportMethod(typeof(TargetFrameworkAttribute).GetConstructors().Single());

		CustomAttributeSignature signature = new();

		signature.FixedArguments.Add(new(module.CorLibTypeFactory.String, module.OriginalTargetRuntime.ToString()));
		signature.NamedArguments.Add(new(
			CustomAttributeArgumentMemberType.Property,
			nameof(TargetFrameworkAttribute.FrameworkDisplayName),
			module.CorLibTypeFactory.String,
			new(module.CorLibTypeFactory.String, ".NET 9.0")));

		CustomAttribute attribute = new((ICustomAttributeType)constructor, signature);

		if (module.Assembly is null)
		{
			AssemblyDefinition assembly = new(module.Name, new Version(1, 0, 0, 0));
			assembly.Modules.Add(module);
			assembly.CustomAttributes.Add(attribute);
		}
		else
		{
			module.Assembly.CustomAttributes.Add(attribute);
		}
	}

	public static ModuleDefinition FixCorLibAssemblyReferences(this ModuleDefinition module)
	{
		MemoryStream stream = new();
		module.Write(stream);
		stream.Position = 0;
		ModuleDefinition module2 = ModuleDefinition.FromBytes(stream.ToArray());
		AssemblyReference? systemPrivateCorLib = module2.AssemblyReferences.FirstOrDefault(a => a.Name == "System.Private.CoreLib");
		AssemblyReference? systemRuntime = module2.AssemblyReferences.FirstOrDefault(a => a.Name == "System.Runtime");
		if (systemPrivateCorLib is not null && systemRuntime is not null)
		{
			foreach (TypeReference typeReference in module2.GetImportedTypeReferences())
			{
				if (typeReference.Scope == systemPrivateCorLib)
				{
					typeReference.Scope = systemRuntime;
				}
			}
			return module2;
		}
		else
		{
			return module;
		}
	}

	public static int GetSize(this TypeSignature type)
	{
		if (type is CorLibTypeSignature corLibTypeSignature)
		{
			return corLibTypeSignature.ElementType switch
			{
				ElementType.U1 => sizeof(byte),
				ElementType.I1 => sizeof(sbyte),
				ElementType.U2 => sizeof(ushort),
				ElementType.I2 => sizeof(short),
				ElementType.U4 => sizeof(uint),
				ElementType.I4 => sizeof(int),
				ElementType.U8 => sizeof(ulong),
				ElementType.I8 => sizeof(long),
				_ => throw new NotSupportedException(),
			};
		}
		else
		{
			throw new NotSupportedException();
		}
	}

	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// Void is treated as 1, for the sake of pointer arithmetic.
	/// </remarks>
	/// <param name="type"></param>
	/// <param name="size"></param>
	/// <returns></returns>
	public static bool TryGetSize(this TypeSignature type, out int size)
	{
		if (type is CorLibTypeSignature corLibTypeSignature)
		{
			int? local = corLibTypeSignature.ElementType switch
			{
				ElementType.U1 => sizeof(byte),
				ElementType.I1 => sizeof(sbyte),
				ElementType.U2 => sizeof(ushort),
				ElementType.I2 => sizeof(short),
				ElementType.U4 => sizeof(uint),
				ElementType.I4 => sizeof(int),
				ElementType.U8 => sizeof(ulong),
				ElementType.I8 => sizeof(long),
				ElementType.Void => 1,
				_ => null,
			};
			if (local.HasValue)
			{
				size = local.Value;
				return true;
			}
			else
			{
				size = 0;
				return false;
			}
		}
		else
		{
			size = 0;
			return false;
		}
	}

	public static void AddBooleanNot(this CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldc_I4_0);
		instructions.Add(CilOpCodes.Ceq);
	}

	public static bool IsLoadConstantInteger(this CilInstruction instruction, out long value)
	{
		if (instruction.IsLdcI4())
		{
			value = instruction.GetLdcI4Constant();
			return true;
		}
		else if (instruction.OpCode == CilOpCodes.Ldc_I8 && instruction.Operand is long i64)
		{
			value = i64;
			return true;
		}
		else
		{
			value = default;
			return false;
		}
	}

	public static bool IsInt32(this TypeSignature type)
	{
		return type is CorLibTypeSignature { ElementType: ElementType.I4 };
	}

	public static bool IsVoid(this TypeSignature type)
	{
		return type is CorLibTypeSignature { ElementType: ElementType.Void };
	}

	public static bool IsVoidPointer(this TypeSignature type)
	{
		return type is PointerTypeSignature { BaseType: CorLibTypeSignature { ElementType: ElementType.Void } };
	}

	public static bool IsNumericPrimitive(this TypeSignature type)
	{
		return type is CorLibTypeSignature { ElementType: ElementType.I1 or ElementType.U1 or ElementType.I2 or ElementType.U2 or ElementType.I4 or ElementType.U4 or ElementType.I8 or ElementType.U8 or ElementType.I or ElementType.U or ElementType.R4 or ElementType.R8 };
	}

	public static bool IsNumeric(this TypeSignature type)
	{
		if (type.IsNumericPrimitive())
		{
			return true;
		}

		if (type is not TypeDefOrRefSignature typeDefOrRefSignature || !typeDefOrRefSignature.IsValueType)
		{
			return false;
		}

		TypeDefinition? typeDefinition = typeDefOrRefSignature.Resolve();
		if (typeDefinition is null)
		{
			return false;
		}

		// Look for INumberBase<T> interface
		foreach (InterfaceImplementation interfaceImplementation in typeDefinition.Interfaces)
		{
			if (interfaceImplementation.Interface?.ToTypeSignature() is not GenericInstanceTypeSignature { TypeArguments.Count: 1 } genericInstanceTypeSignature)
			{
				continue;
			}

			if (genericInstanceTypeSignature.GenericType.Namespace == "System.Numerics" && genericInstanceTypeSignature.GenericType.Name == "INumberBase`1")
			{
				return true;
			}
		}

		return false;
	}

	public static bool TryGetReverseSign(this TypeSignature type, [NotNullWhen(true)] out TypeSignature? opposite)
	{
		if (type is CorLibTypeSignature corLibTypeSignature)
		{
			opposite = corLibTypeSignature.ElementType switch
			{
				ElementType.I1 => corLibTypeSignature.Module?.CorLibTypeFactory.Byte,
				ElementType.U1 => corLibTypeSignature.Module?.CorLibTypeFactory.SByte,
				ElementType.I2 => corLibTypeSignature.Module?.CorLibTypeFactory.UInt16,
				ElementType.U2 => corLibTypeSignature.Module?.CorLibTypeFactory.Int16,
				ElementType.I4 => corLibTypeSignature.Module?.CorLibTypeFactory.UInt32,
				ElementType.U4 => corLibTypeSignature.Module?.CorLibTypeFactory.Int32,
				ElementType.I8 => corLibTypeSignature.Module?.CorLibTypeFactory.UInt64,
				ElementType.U8 => corLibTypeSignature.Module?.CorLibTypeFactory.Int64,
				ElementType.I => corLibTypeSignature.Module?.CorLibTypeFactory.UIntPtr,
				ElementType.U => corLibTypeSignature.Module?.CorLibTypeFactory.IntPtr,
				_ => null,
			};
			return opposite is not null;
		}
		// Could do Int128 and UInt128 here
		else
		{
			opposite = null;
			return false;
		}
	}

	public static TypeSignature ToSignedNumeric(this TypeSignature type)
	{
		if (type is CorLibTypeSignature corLibTypeSignature)
		{
			return corLibTypeSignature.ElementType switch
			{
				ElementType.I1 => corLibTypeSignature,
				ElementType.U1 => corLibTypeSignature.Module?.CorLibTypeFactory.SByte,
				ElementType.I2 => corLibTypeSignature,
				ElementType.U2 => corLibTypeSignature.Module?.CorLibTypeFactory.Int16,
				ElementType.I4 => corLibTypeSignature,
				ElementType.U4 => corLibTypeSignature.Module?.CorLibTypeFactory.Int32,
				ElementType.I8 => corLibTypeSignature,
				ElementType.U8 => corLibTypeSignature.Module?.CorLibTypeFactory.Int64,
				ElementType.I => corLibTypeSignature,
				ElementType.U => corLibTypeSignature.Module?.CorLibTypeFactory.IntPtr,
				_ => null,
			} ?? type;
		}
		else
		{
			return type;
		}
	}

	public static TypeSignature ToUnsignedNumeric(this TypeSignature type)
	{
		if (type is CorLibTypeSignature corLibTypeSignature)
		{
			return corLibTypeSignature.ElementType switch
			{
				ElementType.I1 => corLibTypeSignature.Module?.CorLibTypeFactory.Byte,
				ElementType.U1 => corLibTypeSignature,
				ElementType.I2 => corLibTypeSignature.Module?.CorLibTypeFactory.UInt16,
				ElementType.U2 => corLibTypeSignature,
				ElementType.I4 => corLibTypeSignature.Module?.CorLibTypeFactory.UInt32,
				ElementType.U4 => corLibTypeSignature,
				ElementType.I8 => corLibTypeSignature.Module?.CorLibTypeFactory.UInt64,
				ElementType.U8 => corLibTypeSignature,
				ElementType.I => corLibTypeSignature.Module?.CorLibTypeFactory.UIntPtr,
				ElementType.U => corLibTypeSignature,
				_ => null,
			} ?? type;
		}
		else
		{
			return type;
		}
	}
}
