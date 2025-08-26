using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public abstract record class Instruction
{
	/// <summary>
	/// If true, optimizations cannot make changes that would affect the stack height coming into this instruction.
	/// </summary>
	public virtual bool StackHeightDependent => false;
	public abstract int PopCount { get; }
	public abstract int PushCount { get; }
	public int StackEffect => PushCount - PopCount;
	public abstract void AddInstructions(CilInstructionCollection instructions);
	protected virtual string ToStringImplementation() => GetType().Name;
	public sealed override string ToString() => ToStringImplementation();

	public static Instruction FromOpCode(CilOpCode opCode)
	{
		if (CilOpCodeInstruction.Cache.TryGetValue(opCode, out CilOpCodeInstruction? instruction))
		{
			return instruction;
		}

		if (opCode.OperandType is not CilOperandType.InlineNone)
		{
			throw new NotSupportedException($"Unsupported CIL opcode with operand type: {opCode.OperandType} ({opCode})");
		}

		instruction = new CilOpCodeInstruction(opCode);
		_ = instruction.PopCount; // Ensure PopCount is known
		_ = instruction.PushCount; // Ensure PushCount is known

		CilOpCodeInstruction.Cache[opCode] = instruction;
		return instruction;
	}

	private sealed record class CilOpCodeInstruction(CilOpCode OpCode) : Instruction
	{
		internal static readonly Dictionary<CilOpCode, CilOpCodeInstruction> Cache = new();

		public override int PopCount => OpCode.StackBehaviourPop switch
		{
			CilStackBehaviour.Pop0 => 0,
			CilStackBehaviour.Pop1 or CilStackBehaviour.PopI or CilStackBehaviour.PopRef => 1,
			CilStackBehaviour.Pop1_Pop1 or CilStackBehaviour.PopI_Pop1 or CilStackBehaviour.PopI_PopI or CilStackBehaviour.PopI_PopI8 or CilStackBehaviour.PopI_PopR4 or CilStackBehaviour.PopI_PopR8 or CilStackBehaviour.PopRef_Pop1 or CilStackBehaviour.PopRef_PopI => 2,
			CilStackBehaviour.PopI_PopI_PopI or CilStackBehaviour.PopRef_PopI_PopI or CilStackBehaviour.PopRef_PopI_PopI8 or CilStackBehaviour.PopRef_PopI_PopR4 or CilStackBehaviour.PopRef_PopI_PopR8 or CilStackBehaviour.PopRef_PopI_PopRef or CilStackBehaviour.PopRef_PopI_Pop1 => 3,
			_ => throw new ArgumentOutOfRangeException(),
		};

		public override int PushCount => OpCode.StackBehaviourPush switch
		{
			CilStackBehaviour.Push0 => 0,
			CilStackBehaviour.Push1 or CilStackBehaviour.PushI or CilStackBehaviour.PushI8 or CilStackBehaviour.PushR4 or CilStackBehaviour.PushR8 or CilStackBehaviour.PushRef => 1,
			CilStackBehaviour.Push1_Push1 => 2,
			_ => throw new ArgumentOutOfRangeException(),
		};

		public override void AddInstructions(CilInstructionCollection instructions)
		{
			instructions.Add(OpCode);
		}

		protected override string ToStringImplementation() => OpCode.Code.ToString();
	}
}
