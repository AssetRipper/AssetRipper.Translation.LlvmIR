using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Variables;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public static class NumericalComparison
{
	public static new NumericalComparisonInstruction Equals { get; } = new EqualsComparisonInstruction();
	public static NumericalComparisonInstruction NotEquals { get; } = new NotEqualsComparisonInstruction();
	public static NumericalComparisonInstruction GreaterThan { get; } = new GreaterThanComparisonInstruction();
	public static NumericalComparisonInstruction GreaterThanOrEquals { get; } = new GreaterThanOrEqualsComparisonInstruction();
	public static NumericalComparisonInstruction LessThan { get; } = new LessThanComparisonInstruction();
	public static NumericalComparisonInstruction LessThanOrEquals { get; } = new LessThanOrEqualsComparisonInstruction();
	public static NumericalComparisonInstruction UnsignedGreaterThan { get; } = new UnsignedGreaterThanComparisonInstruction();
	public static NumericalComparisonInstruction UnsignedGreaterThanOrEquals { get; } = new UnsignedGreaterThanOrEqualsComparisonInstruction();
	public static NumericalComparisonInstruction UnsignedLessThan { get; } = new UnsignedLessThanComparisonInstruction();
	public static NumericalComparisonInstruction UnsignedLessThanOrEquals { get; } = new UnsignedLessThanOrEqualsComparisonInstruction();

	public static NumericalComparisonInstruction Create(TypeSignature type, LLVMIntPredicate comparisonKind)
	{
		if (type is not CorLibTypeSignature and not PointerTypeSignature)
		{
			throw new NotSupportedException($"Unsupported type for integer comparison: {type}");
		}
		return comparisonKind switch
		{
			LLVMIntPredicate.LLVMIntEQ => Equals,
			LLVMIntPredicate.LLVMIntNE => NotEquals,
			LLVMIntPredicate.LLVMIntUGT => UnsignedGreaterThan,
			LLVMIntPredicate.LLVMIntUGE => UnsignedGreaterThanOrEquals,
			LLVMIntPredicate.LLVMIntULT => UnsignedLessThan,
			LLVMIntPredicate.LLVMIntULE => UnsignedLessThanOrEquals,
			LLVMIntPredicate.LLVMIntSGT => GreaterThan,
			LLVMIntPredicate.LLVMIntSGE => GreaterThanOrEquals,
			LLVMIntPredicate.LLVMIntSLT => LessThan,
			LLVMIntPredicate.LLVMIntSLE => LessThanOrEquals,
			_ => throw new InvalidOperationException($"Unknown comparison predicate: {comparisonKind}"),
		};
	}

	internal static Instruction Create(TypeSignature type, LLVMRealPredicate comparisonKind, ModuleContext module)
	{
		if (type is not CorLibTypeSignature { ElementType: ElementType.R4 or ElementType.R8 })
		{
			throw new NotSupportedException($"Unsupported type for float comparison: {type}");
		}
		// https://github.com/llvm/llvm-project/blob/070f3310ccfd040271aae982f36c008f2c34e11d/llvm/include/llvm-c/Core.h#L307-L324
		return comparisonKind switch
		{
			LLVMRealPredicate.LLVMRealOEQ => Equals,
			LLVMRealPredicate.LLVMRealONE => NotEquals,
			LLVMRealPredicate.LLVMRealOGT => GreaterThan,
			LLVMRealPredicate.LLVMRealOGE => GreaterThanOrEquals,
			LLVMRealPredicate.LLVMRealOLT => LessThan,
			LLVMRealPredicate.LLVMRealOLE => LessThanOrEquals,

			LLVMRealPredicate.LLVMRealORD => Call(nameof(NumericHelper.IsOrdered), type, module),
			LLVMRealPredicate.LLVMRealUNO => Call(nameof(NumericHelper.IsUnordered), type, module),

			LLVMRealPredicate.LLVMRealUEQ => Call(nameof(NumericHelper.IsUnorderedOrEquals), type, module),
			LLVMRealPredicate.LLVMRealUNE => Call(nameof(NumericHelper.IsUnorderedOrNotEquals), type, module),
			LLVMRealPredicate.LLVMRealUGT => Call(nameof(NumericHelper.IsUnorderedOrGreaterThan), type, module),
			LLVMRealPredicate.LLVMRealUGE => Call(nameof(NumericHelper.IsUnorderedOrGreaterThanOrEquals), type, module),
			LLVMRealPredicate.LLVMRealULT => Call(nameof(NumericHelper.IsUnorderedOrLessThan), type, module),
			LLVMRealPredicate.LLVMRealULE => Call(nameof(NumericHelper.IsUnorderedOrLessThanOrEquals), type, module),

			LLVMRealPredicate.LLVMRealPredicateTrue => new LoadVariableInstruction(new ConstantI4(1, module.Definition)),
			LLVMRealPredicate.LLVMRealPredicateFalse => new LoadVariableInstruction(new ConstantI4(0, module.Definition)),

			_ => throw new InvalidOperationException($"Unknown comparison predicate: {comparisonKind}"),
		};

		static Instruction Call(string methodName, TypeSignature type, ModuleContext module)
		{
			return new CallInstruction(module.NumericHelperType.Methods.First(m => m.Name == methodName).MakeGenericInstanceMethod(type));
		}
	}

	#region Individual Implementations
	private sealed record class EqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Ceq);
		}
	}

	private sealed record class NotEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Ceq);
			instructions.AddBooleanNot();
		}
	}

	private sealed record class GreaterThanComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Cgt);
		}
	}

	private sealed record class GreaterThanOrEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Clt);
			instructions.AddBooleanNot();
		}
	}

	private sealed record class LessThanComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Clt);
		}
	}

	private sealed record class LessThanOrEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Cgt);
			instructions.AddBooleanNot();
		}
	}

	private sealed record class UnsignedGreaterThanComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Cgt_Un);
		}
	}

	private sealed record class UnsignedGreaterThanOrEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Clt_Un);
			instructions.AddBooleanNot();
		}
	}

	private sealed record class UnsignedLessThanComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Clt_Un);
		}
	}

	private sealed record class UnsignedLessThanOrEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Cgt_Un);
			instructions.AddBooleanNot();
		}
	}
	#endregion
}
