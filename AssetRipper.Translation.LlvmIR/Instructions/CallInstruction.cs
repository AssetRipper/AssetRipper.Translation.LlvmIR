using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class CallInstruction(IMethodDescriptor Method) : Instruction
{
	public bool IsStatic => !Method.Signature!.HasThis;
	public int ParameterCount => Method.Signature!.ParameterTypes.Count;
	public bool ReturnsValue => Method.Signature!.ReturnsValue;
	public override int PopCount => ParameterCount + (IsStatic ? 0 : 1);
	public override int PushCount => ReturnsValue ? 1 : 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(IsStatic ? CilOpCodes.Call : CilOpCodes.Callvirt, Method);
	}

	public bool Equals(CallInstruction? other)
	{
		if (other is null)
		{
			return false;
		}
		return SignatureComparer.Default.Equals(Method, other.Method);
	}

	public override int GetHashCode()
	{
		return SignatureComparer.Default.GetHashCode(Method);
	}

	protected override string ToStringImplementation()
	{
		return $"CallInstruction {{ {Method} }}";
	}
}
