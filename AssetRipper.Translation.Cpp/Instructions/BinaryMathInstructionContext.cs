using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class BinaryMathInstructionContext : InstructionContext
{
	internal BinaryMathInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 2);
	}
	public LLVMValueRef Operand1 => Operands[0];
	public LLVMValueRef Operand2 => Operands[1];
	public CilOpCode CilOpCode => Opcode switch
	{
		LLVMOpcode.LLVMAdd => NoSignedWrap
			? CilOpCodes.Add_Ovf
			: NoUnsignedWrap
				? CilOpCodes.Add_Ovf_Un
				: CilOpCodes.Add,
		LLVMOpcode.LLVMFAdd => CilOpCodes.Add,
		LLVMOpcode.LLVMSub => NoSignedWrap
			? CilOpCodes.Sub_Ovf
			: NoUnsignedWrap
				? CilOpCodes.Sub_Ovf_Un
				: CilOpCodes.Sub,
		LLVMOpcode.LLVMFSub => CilOpCodes.Sub,
		LLVMOpcode.LLVMMul => NoSignedWrap
			? CilOpCodes.Mul_Ovf
			: NoUnsignedWrap
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

	public string Name => Opcode switch
	{
		LLVMOpcode.LLVMAdd => NoSignedWrap
			? "AddSigned"
			: NoUnsignedWrap
				? "AddUnsigned"
				: "Add",
		LLVMOpcode.LLVMFAdd => "Add",
		LLVMOpcode.LLVMSub => NoSignedWrap
			? "SubtractSigned"
			: NoUnsignedWrap
				? "SubtractUnsigned"
				: "Subtract",
		LLVMOpcode.LLVMFSub => "Subtract",
		LLVMOpcode.LLVMMul => NoSignedWrap
			? "MultiplySigned"
			: NoUnsignedWrap
				? "MultiplyUnsigned"
				: "Multiply",
		LLVMOpcode.LLVMFMul => "Multiply",
		LLVMOpcode.LLVMSDiv or LLVMOpcode.LLVMFDiv or LLVMOpcode.LLVMUDiv => "Divide", // Might need more specificity
		LLVMOpcode.LLVMSRem or LLVMOpcode.LLVMFRem or LLVMOpcode.LLVMURem => "Remainder", // Might need more specificity
		LLVMOpcode.LLVMShl => "LeftShift",
		LLVMOpcode.LLVMLShr => "RightShiftLogical",
		LLVMOpcode.LLVMAShr => "RightShiftArithmetic",
		LLVMOpcode.LLVMAnd => "And",
		LLVMOpcode.LLVMOr => "Or",
		LLVMOpcode.LLVMXor => "Xor",
		_ => throw new NotSupportedException(),
	};

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

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Module.LoadValue(instructions, Operand1);
		Module.LoadValue(instructions, Operand2);
		if (ResultTypeSignature is CorLibTypeSignature)
		{
			instructions.Add(CilOpCode);
		}
		else if (ResultTypeSignature is TypeDefOrRefSignature)
		{
			TypeDefinition type = ResultTypeSignature.Resolve() ?? throw new NullReferenceException(nameof(type));
			string methodName = Name;
			if (Module.InlineArrayTypes.TryGetValue(type, out InlineArrayContext? arrayType))
			{
				MethodDefinition method = Module.InlineArrayNumericHelperType.Methods.First(m => m.Name == methodName);

				arrayType.GetUltimateElementType(out TypeSignature elementType, out _);

				TypeSignature typeParameter;
				if (NoSignedWrap)
				{
					typeParameter = elementType.ToSignedNumeric();
				}
				else if (NoUnsignedWrap)
				{
					typeParameter = elementType.ToUnsignedNumeric();
				}
				else
				{
					typeParameter = elementType;
				}

				IMethodDescriptor methodDescriptor = method.MakeGenericInstanceMethod(ResultTypeSignature, typeParameter);

				instructions.Add(CilOpCodes.Call, methodDescriptor);
			}
			else
			{
				MethodDefinition method = Module.NumericHelperType.Methods.First(m => m.Name == methodName);

				TypeSignature typeParameter;
				if (NoSignedWrap)
				{
					typeParameter = ResultTypeSignature.ToSignedNumeric();
				}
				else if (NoUnsignedWrap)
				{
					typeParameter = ResultTypeSignature.ToUnsignedNumeric();
				}
				else
				{
					typeParameter = ResultTypeSignature;
				}

				IMethodDescriptor methodDescriptor = method.MakeGenericInstanceMethod(typeParameter);

				instructions.Add(CilOpCodes.Call, methodDescriptor);
			}
		}
		else
		{
			throw new NotSupportedException();
		}
		instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
	}
}
