using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace AssetRipper.Translation.Cpp;

internal static class AsmResolverExtensions
{
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

	public static void AddLoadIndirect(this CilInstructionCollection instructions, TypeSignature type)
	{
		switch (type)
		{
			case CorLibTypeSignature corLibTypeSignature:
				switch (corLibTypeSignature.ElementType)
				{
					case ElementType.I1:
						instructions.Add(CilOpCodes.Ldind_I1);
						break;
					case ElementType.I2:
						instructions.Add(CilOpCodes.Ldind_I2);
						break;
					case ElementType.I4:
						instructions.Add(CilOpCodes.Ldind_I4);
						break;
					case ElementType.I8:
					case ElementType.U8:
						instructions.Add(CilOpCodes.Ldind_I8);
						break;
					case ElementType.U1:
						instructions.Add(CilOpCodes.Ldind_U1);
						break;
					case ElementType.U2:
						instructions.Add(CilOpCodes.Ldind_U2);
						break;
					case ElementType.U4:
						instructions.Add(CilOpCodes.Ldind_U4);
						break;
					case ElementType.R4:
						instructions.Add(CilOpCodes.Ldind_R4);
						break;
					case ElementType.R8:
						instructions.Add(CilOpCodes.Ldind_R8);
						break;
					case ElementType.I:
					case ElementType.U:
						instructions.Add(CilOpCodes.Ldind_I);
						break;
					default:
						instructions.Add(CilOpCodes.Ldobj, type.ToTypeDefOrRef());
						break;
				}
				break;
			case PointerTypeSignature:
				instructions.Add(CilOpCodes.Ldind_I);
				break;
			default:
				instructions.Add(CilOpCodes.Ldobj, type.ToTypeDefOrRef());
				break;
		}
	}

	public static void AddStoreIndirect(this CilInstructionCollection instructions, TypeSignature type)
	{
		switch (type)
		{
			case CorLibTypeSignature corLibTypeSignature:
				switch (corLibTypeSignature.ElementType)
				{
					case ElementType.I1:
						instructions.Add(CilOpCodes.Stind_I1);
						break;
					case ElementType.I2:
						instructions.Add(CilOpCodes.Stind_I2);
						break;
					case ElementType.I4:
						instructions.Add(CilOpCodes.Stind_I4);
						break;
					case ElementType.I8:
					case ElementType.U8:
						instructions.Add(CilOpCodes.Stind_I8);
						break;
					case ElementType.U1:
						instructions.Add(CilOpCodes.Stind_I1);
						break;
					case ElementType.U2:
						instructions.Add(CilOpCodes.Stind_I2);
						break;
					case ElementType.U4:
						instructions.Add(CilOpCodes.Stind_I4);
						break;
					case ElementType.R4:
						instructions.Add(CilOpCodes.Stind_R4);
						break;
					case ElementType.R8:
						instructions.Add(CilOpCodes.Stind_R8);
						break;
					case ElementType.I:
					case ElementType.U:
						instructions.Add(CilOpCodes.Stind_I);
						break;
					default:
						instructions.Add(CilOpCodes.Stobj, type.ToTypeDefOrRef());
						break;
				}
				break;
			case PointerTypeSignature:
				instructions.Add(CilOpCodes.Stind_I);
				break;
			default:
				instructions.Add(CilOpCodes.Stobj, type.ToTypeDefOrRef());
				break;
		}
	}
}
