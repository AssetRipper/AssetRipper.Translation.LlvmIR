using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class GetElementPointerInstructionContext : InstructionContext
{
	internal GetElementPointerInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length >= 2);

		SourceElementTypeSignature = module.GetTypeSignature(SourceElementType);
		FinalType = CalculateFinalType();
		ResultTypeSignature = FinalType.MakePointerType();
	}
	/// <summary>
	/// This is the pointer. It's generally void* due to stripping.
	/// </summary>
	public LLVMValueRef SourceOperand => Operands[0];
	public unsafe LLVMTypeRef SourceElementType => LLVM.GetGEPSourceElementType(Instruction);
	public TypeSignature SourceElementTypeSignature { get; set; }
	public TypeSignature FinalType { get; set; }
	public ReadOnlySpan<LLVMValueRef> IndexOperands => Operands.AsSpan()[1..];

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		//This is the pointer. It's generally void* due to stripping.
		LoadValue(instructions, SourceOperand);//Pointer

		//This isn't strictly necessary, but it might make ILSpy output better someday.
		CilLocalVariable pointerLocal = instructions.AddLocalVariable(SourceElementTypeSignature.MakePointerType());
		instructions.Add(CilOpCodes.Stloc, pointerLocal);
		instructions.Add(CilOpCodes.Ldloc, pointerLocal);

		//This is the index, which might be a constant.
		LoadValue(instructions, Operands[1]);
		CilInstruction previousInstruction = instructions[^1];

		if (previousInstruction.IsLoadConstantInteger(out long value) && value == 0)
		{
			//Remove the Ldc_I4_0
			//There's no need to add it to the pointer.
			instructions.Pop();
		}
		else if (SourceElementTypeSignature.TryGetSize(out int size))
		{
			if (size == 1)
			{
				instructions.Add(CilOpCodes.Conv_I);
			}
			else
			{
				if (previousInstruction.IsLoadConstantInteger(out value))
				{
					previousInstruction.OpCode = CilOpCodes.Ldc_I4;
					previousInstruction.Operand = (int)(value * size);
					instructions.Add(CilOpCodes.Conv_I);
				}
				else
				{
					instructions.Add(CilOpCodes.Conv_I);
					instructions.Add(CilOpCodes.Ldc_I4, size);
					instructions.Add(CilOpCodes.Mul);
				}
			}
			instructions.Add(CilOpCodes.Add);
		}
		else
		{
			instructions.Add(CilOpCodes.Conv_I);
			instructions.Add(CilOpCodes.Sizeof, SourceElementTypeSignature.ToTypeDefOrRef());
			instructions.Add(CilOpCodes.Mul);
			instructions.Add(CilOpCodes.Add);
		}

		TypeSignature currentType = SourceElementTypeSignature;
		for (int i = 2; i < Operands.Length; i++)
		{
			LLVMValueRef operand = Operands[i];
			if (currentType is TypeDefOrRefSignature structTypeSignature)
			{
				TypeDefinition structType = (TypeDefinition)structTypeSignature.ToTypeDefOrRef();
				if (Module.InlineArrayTypes.TryGetValue(structType, out (TypeSignature, int) pair))
				{
					currentType = pair.Item1;
					instructions.Add(CilOpCodes.Sizeof, currentType.ToTypeDefOrRef());
					LoadValue(instructions, operand);
					instructions.Add(CilOpCodes.Conv_I4);
					instructions.Add(CilOpCodes.Mul);
					instructions.Add(CilOpCodes.Add);
				}
				else
				{
					if (operand.Kind == LLVMValueKind.LLVMConstantIntValueKind)
					{
						long index = operand.ConstIntSExt;
						string fieldName = $"field_{index}";
						FieldDefinition field = structType.Fields.First(t => t.Name == fieldName);
						instructions.Add(CilOpCodes.Ldflda, field);
						currentType = field.Signature!.FieldType;
					}
					else
					{
						throw new NotSupportedException();
					}
				}
			}
			else if (currentType is CorLibTypeSignature)
			{
				throw new NotSupportedException();
			}
			else
			{
				throw new NotSupportedException();
			}
		}

		instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
	}

	private TypeSignature CalculateFinalType()
	{
		TypeSignature currentType = SourceElementTypeSignature;
		for (int i = 2; i < Operands.Length; i++)
		{
			LLVMValueRef operand = Operands[i];
			if (currentType is TypeDefOrRefSignature structTypeSignature)
			{
				TypeDefinition structType = (TypeDefinition)structTypeSignature.ToTypeDefOrRef();
				if (Module.InlineArrayTypes.TryGetValue(structType, out (TypeSignature, int) pair))
				{
					currentType = pair.Item1;
				}
				else if (operand.Kind == LLVMValueKind.LLVMConstantIntValueKind)
				{
					long index = operand.ConstIntSExt;
					string fieldName = $"field_{index}";
					FieldDefinition field = structType.Fields.First(t => t.Name == fieldName);
					currentType = field.Signature!.FieldType;
				}
				else
				{
					throw new NotSupportedException();
				}
			}
			else if (currentType is CorLibTypeSignature)
			{
				throw new NotSupportedException();
			}
			else
			{
				throw new NotSupportedException();
			}
		}
		return currentType;
	}
}
