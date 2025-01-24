using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
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
}
