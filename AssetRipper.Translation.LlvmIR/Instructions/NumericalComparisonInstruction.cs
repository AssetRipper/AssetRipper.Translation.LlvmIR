using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public abstract class NumericalComparisonInstruction : Instruction
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
	public override int PopCount => 2;
	public override int PushCount => 1;
	private NumericalComparisonInstruction()
	{
	}

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

	public static NumericalComparisonInstruction Create(TypeSignature type, LLVMRealPredicate comparisonKind)
	{
		if (type is not CorLibTypeSignature { ElementType: ElementType.R4 or ElementType.R8 })
		{
			throw new NotSupportedException($"Unsupported type for float comparison: {type}");
		}
		return comparisonKind switch
		{
			LLVMRealPredicate.LLVMRealOEQ or LLVMRealPredicate.LLVMRealUEQ => Equals,
			LLVMRealPredicate.LLVMRealONE or LLVMRealPredicate.LLVMRealUNE => NotEquals,
			LLVMRealPredicate.LLVMRealUGT => UnsignedGreaterThan,
			LLVMRealPredicate.LLVMRealUGE => UnsignedGreaterThanOrEquals,
			LLVMRealPredicate.LLVMRealULT => UnsignedLessThan,
			LLVMRealPredicate.LLVMRealULE => UnsignedLessThanOrEquals,
			LLVMRealPredicate.LLVMRealOGT => GreaterThan,
			LLVMRealPredicate.LLVMRealOGE => GreaterThanOrEquals,
			LLVMRealPredicate.LLVMRealOLT => LessThan,
			LLVMRealPredicate.LLVMRealOLE => LessThanOrEquals,
			_ => throw new InvalidOperationException($"Unknown comparison predicate: {comparisonKind}"),
		};
	}

	#region Individual Implementations
	private sealed class EqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Ceq);
		}
	}

	private sealed class NotEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Ceq);
			instructions.AddBooleanNot();
		}
	}

	private sealed class GreaterThanComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Cgt);
		}
	}

	private sealed class GreaterThanOrEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Clt);
			instructions.AddBooleanNot();
		}
	}

	private sealed class LessThanComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Clt);
		}
	}

	private sealed class LessThanOrEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Cgt);
			instructions.AddBooleanNot();
		}
	}

	private sealed class UnsignedGreaterThanComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Cgt_Un);
		}
	}

	private sealed class UnsignedGreaterThanOrEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Clt_Un);
			instructions.AddBooleanNot();
		}
	}

	private sealed class UnsignedLessThanComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Clt_Un);
		}
	}

	private sealed class UnsignedLessThanOrEqualsComparisonInstruction : NumericalComparisonInstruction
	{
		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(CilOpCodes.Cgt_Un);
			instructions.AddBooleanNot();
		}
	}
	#endregion
}
