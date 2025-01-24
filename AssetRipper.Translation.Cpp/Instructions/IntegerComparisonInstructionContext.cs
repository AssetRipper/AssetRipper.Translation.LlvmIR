using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class IntegerComparisonInstructionContext : NumericComparisonInstructionContext
{
	internal IntegerComparisonInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
	}

	public override void AddComparison(CilInstructionCollection instructions)
	{
		switch (Instruction.ICmpPredicate)
		{
			case LLVMIntPredicate.LLVMIntEQ:
				instructions.Add(CilOpCodes.Ceq);
				break;
			case LLVMIntPredicate.LLVMIntNE:
				instructions.Add(CilOpCodes.Ceq);
				instructions.AddBooleanNot();
				break;
			case LLVMIntPredicate.LLVMIntUGT:
				instructions.Add(CilOpCodes.Cgt_Un);
				break;
			case LLVMIntPredicate.LLVMIntUGE:
				instructions.Add(CilOpCodes.Clt_Un);
				instructions.AddBooleanNot();
				break;
			case LLVMIntPredicate.LLVMIntULT:
				instructions.Add(CilOpCodes.Clt_Un);
				break;
			case LLVMIntPredicate.LLVMIntULE:
				instructions.Add(CilOpCodes.Cgt_Un);
				instructions.AddBooleanNot();
				break;
			case LLVMIntPredicate.LLVMIntSGT:
				instructions.Add(CilOpCodes.Cgt);
				break;
			case LLVMIntPredicate.LLVMIntSGE:
				instructions.Add(CilOpCodes.Clt);
				instructions.AddBooleanNot();
				break;
			case LLVMIntPredicate.LLVMIntSLT:
				instructions.Add(CilOpCodes.Clt);
				break;
			case LLVMIntPredicate.LLVMIntSLE:
				instructions.Add(CilOpCodes.Cgt);
				instructions.AddBooleanNot();
				break;
			default:
				throw new InvalidOperationException($"Unknown comparison predicate: {Instruction.ICmpPredicate}");
		};
	}
}
