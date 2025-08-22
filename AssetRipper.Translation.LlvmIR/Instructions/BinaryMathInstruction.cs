using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal static class BinaryMathInstruction
{
	public static Instruction Create(LLVMValueRef instruction, ModuleContext Module)
	{
		LLVMOpcode opcode = instruction.GetOpcode();
		Debug.Assert(Supported(opcode), $"Unsupported binary math instruction: {opcode}");

		TypeSignature resultTypeSignature = Module.GetTypeSignature(instruction);
		bool noSignedWrap = LibLLVMSharp.InstructionHasNoSignedWrap(instruction);
		bool noUnsignedWrap = LibLLVMSharp.InstructionHasNoUnsignedWrap(instruction);

		if (resultTypeSignature is CorLibTypeSignature)
		{
			return Instruction.FromOpCode(GetCilOpCode(opcode, noSignedWrap, noUnsignedWrap));
		}
		else if (resultTypeSignature is TypeDefOrRefSignature)
		{
			TypeDefinition type = resultTypeSignature.Resolve() ?? throw new NullReferenceException(nameof(type));
			string methodName = GetName(opcode, noSignedWrap, noUnsignedWrap);
			if (Module.InlineArrayTypes.TryGetValue(type, out InlineArrayContext? arrayType))
			{
				MethodDefinition method = Module.InlineArrayNumericHelperType.Methods.First(m => m.Name == methodName);

				arrayType.GetUltimateElementType(out TypeSignature elementType, out _);

				TypeSignature typeParameter;
				if (noSignedWrap)
				{
					typeParameter = elementType.ToSignedNumeric();
				}
				else if (noUnsignedWrap)
				{
					typeParameter = elementType.ToUnsignedNumeric();
				}
				else
				{
					typeParameter = elementType;
				}

				IMethodDescriptor methodDescriptor = method.MakeGenericInstanceMethod(resultTypeSignature, typeParameter);

				return new CallInstruction(methodDescriptor);
			}
			else
			{
				MethodDefinition method = Module.NumericHelperType.Methods.First(m => m.Name == methodName);

				TypeSignature typeParameter;
				if (noSignedWrap)
				{
					typeParameter = resultTypeSignature.ToSignedNumeric();
				}
				else if (noUnsignedWrap)
				{
					typeParameter = resultTypeSignature.ToUnsignedNumeric();
				}
				else
				{
					typeParameter = resultTypeSignature;
				}

				IMethodDescriptor methodDescriptor = method.MakeGenericInstanceMethod(typeParameter);

				return new CallInstruction(methodDescriptor);
			}
		}
		else
		{
			throw new NotSupportedException($"Unsupported result type signature: {resultTypeSignature}");
		}
	}

	public static bool Supported(LLVMOpcode opcode) => opcode switch
	{
		LLVMOpcode.LLVMAdd or LLVMOpcode.LLVMFAdd or
		LLVMOpcode.LLVMSub or LLVMOpcode.LLVMFSub or
		LLVMOpcode.LLVMMul or LLVMOpcode.LLVMFMul or
		LLVMOpcode.LLVMSDiv or LLVMOpcode.LLVMFDiv or
		LLVMOpcode.LLVMSRem or LLVMOpcode.LLVMFRem or
		LLVMOpcode.LLVMUDiv or LLVMOpcode.LLVMURem or
		LLVMOpcode.LLVMShl or LLVMOpcode.LLVMLShr or LLVMOpcode.LLVMAShr or
		LLVMOpcode.LLVMAnd or LLVMOpcode.LLVMOr or LLVMOpcode.LLVMXor => true,
		_ => false,
	};

	private static CilOpCode GetCilOpCode(LLVMOpcode npcode, bool noSignedWrap, bool noUnsignedWrap) => npcode switch
	{
		LLVMOpcode.LLVMAdd => noSignedWrap
			? CilOpCodes.Add_Ovf
			: noUnsignedWrap
				? CilOpCodes.Add_Ovf_Un
				: CilOpCodes.Add,
		LLVMOpcode.LLVMFAdd => CilOpCodes.Add,
		LLVMOpcode.LLVMSub => noSignedWrap
			? CilOpCodes.Sub_Ovf
			: noUnsignedWrap
				? CilOpCodes.Sub_Ovf_Un
				: CilOpCodes.Sub,
		LLVMOpcode.LLVMFSub => CilOpCodes.Sub,
		LLVMOpcode.LLVMMul => noSignedWrap
			? CilOpCodes.Mul_Ovf
			: noUnsignedWrap
				? CilOpCodes.Mul_Ovf_Un
				: CilOpCodes.Mul,
		LLVMOpcode.LLVMFMul => CilOpCodes.Mul,
		LLVMOpcode.LLVMSDiv or LLVMOpcode.LLVMFDiv => CilOpCodes.Div,
		LLVMOpcode.LLVMSRem or LLVMOpcode.LLVMFRem => CilOpCodes.Rem,
		LLVMOpcode.LLVMUDiv => CilOpCodes.Div_Un,
		LLVMOpcode.LLVMURem => CilOpCodes.Rem_Un,
		LLVMOpcode.LLVMShl => CilOpCodes.Shl,
		LLVMOpcode.LLVMLShr => CilOpCodes.Shr_Un,//Logical
		LLVMOpcode.LLVMAShr => CilOpCodes.Shr,//Arithmetic
		LLVMOpcode.LLVMAnd => CilOpCodes.And,
		LLVMOpcode.LLVMOr => CilOpCodes.Or,
		LLVMOpcode.LLVMXor => CilOpCodes.Xor,
		_ => throw new NotSupportedException(),
	};

	private static string GetName(LLVMOpcode opcode, bool noSignedWrap, bool noUnsignedWrap) => opcode switch
	{
		LLVMOpcode.LLVMAdd => noSignedWrap
			? nameof(NumericHelper.AddSigned)
			: noUnsignedWrap
				? nameof(NumericHelper.AddUnsigned)
				: nameof(NumericHelper.Add),
		LLVMOpcode.LLVMFAdd => nameof(NumericHelper.Add),
		LLVMOpcode.LLVMSub => noSignedWrap
			? nameof(NumericHelper.SubtractSigned)
			: noUnsignedWrap
				? nameof(NumericHelper.SubtractUnsigned)
				: nameof(NumericHelper.Subtract),
		LLVMOpcode.LLVMFSub => nameof(NumericHelper.Subtract),
		LLVMOpcode.LLVMMul => noSignedWrap
			? nameof(NumericHelper.MultiplySigned)
			: noUnsignedWrap
				? nameof(NumericHelper.MultiplyUnsigned)
				: nameof(NumericHelper.Multiply),
		LLVMOpcode.LLVMFMul => nameof(NumericHelper.Multiply),
		LLVMOpcode.LLVMFDiv => nameof(NumericHelper.Divide),
		LLVMOpcode.LLVMSDiv => nameof(NumericHelper.DivideSigned),
		LLVMOpcode.LLVMUDiv => nameof(NumericHelper.DivideUnsigned),
		LLVMOpcode.LLVMFRem => nameof(NumericHelper.Remainder),
		LLVMOpcode.LLVMSRem => nameof(NumericHelper.RemainderSigned),
		LLVMOpcode.LLVMURem => nameof(NumericHelper.RemainderUnsigned),
		LLVMOpcode.LLVMShl => nameof(NumericHelper.ShiftLeft),
		LLVMOpcode.LLVMLShr => nameof(NumericHelper.ShiftRightLogical),
		LLVMOpcode.LLVMAShr => nameof(NumericHelper.ShiftRightArithmetic),
		LLVMOpcode.LLVMAnd => nameof(NumericHelper.BitwiseAnd),
		LLVMOpcode.LLVMOr => nameof(NumericHelper.BitwiseOr),
		LLVMOpcode.LLVMXor => nameof(NumericHelper.BitwiseXor),
		_ => throw new NotSupportedException(),
	};
}
