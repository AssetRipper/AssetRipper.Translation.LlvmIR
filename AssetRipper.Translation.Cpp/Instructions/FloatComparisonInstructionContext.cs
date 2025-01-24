using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class FloatComparisonInstructionContext : NumericComparisonInstructionContext
{
	internal FloatComparisonInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
	}

	public override void AddComparison(CilInstructionCollection instructions)
	{
		switch (Instruction.FCmpPredicate)
		{
			case LLVMRealPredicate.LLVMRealOEQ:
			case LLVMRealPredicate.LLVMRealUEQ:
				instructions.Add(CilOpCodes.Ceq);
				break;
			case LLVMRealPredicate.LLVMRealONE:
			case LLVMRealPredicate.LLVMRealUNE:
				instructions.Add(CilOpCodes.Ceq);
				instructions.AddBooleanNot();
				break;
			case LLVMRealPredicate.LLVMRealUGT:
				instructions.Add(CilOpCodes.Cgt_Un);
				break;
			case LLVMRealPredicate.LLVMRealUGE:
				instructions.Add(CilOpCodes.Clt_Un);
				instructions.AddBooleanNot();
				break;
			case LLVMRealPredicate.LLVMRealULT:
				instructions.Add(CilOpCodes.Clt_Un);
				break;
			case LLVMRealPredicate.LLVMRealULE:
				instructions.Add(CilOpCodes.Cgt_Un);
				instructions.AddBooleanNot();
				break;
			case LLVMRealPredicate.LLVMRealOGT:
				instructions.Add(CilOpCodes.Cgt);
				break;
			case LLVMRealPredicate.LLVMRealOGE:
				instructions.Add(CilOpCodes.Clt);
				instructions.AddBooleanNot();
				break;
			case LLVMRealPredicate.LLVMRealOLT:
				instructions.Add(CilOpCodes.Clt);
				break;
			case LLVMRealPredicate.LLVMRealOLE:
				instructions.Add(CilOpCodes.Cgt);
				instructions.AddBooleanNot();
				break;
			default:
				throw new NotImplementedException($"Unknown comparison predicate: {Instruction.ICmpPredicate}");
		};
	}
}
