using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal sealed class GetElementPointerInstructionContext : InstructionContext
{
	internal GetElementPointerInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Operands.Length >= 2);
		Debug.Assert(Operands[0].IsInstruction());
	}
	/// <summary>
	/// This is the pointer. It's generally void* due to stripping.
	/// </summary>
	public LLVMValueRef SourceOperand => Operands[0];
	public unsafe LLVMTypeRef SourceElementType => LLVM.GetGEPSourceElementType(Instruction);
	public TypeSignature SourceElementTypeSignature { get; set; } = null!; // Set during Analysis
	public ReadOnlySpan<LLVMValueRef> IndexOperands => Operands.AsSpan()[1..];

	public TypeSignature CalculateFinalType()
	{
		TypeSignature currentType = SourceElementTypeSignature;
		for (int i = 2; i < Operands.Length; i++)
		{
			LLVMValueRef operand = Operands[i];
			if (currentType is TypeDefOrRefSignature structTypeSignature)
			{
				TypeDefinition structType = (TypeDefinition)structTypeSignature.ToTypeDefOrRef();
				if (operand.Kind == LLVMValueKind.LLVMConstantIntValueKind)
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
