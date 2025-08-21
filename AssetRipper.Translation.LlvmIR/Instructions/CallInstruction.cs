using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class CallInstruction(IMethodDescriptor method) : Instruction
{
	public IMethodDescriptor Method { get; } = method;
	public bool IsStatic => !Method.Signature!.HasThis;
	public int ParameterCount => Method.Signature!.ParameterTypes.Count;
	public bool ReturnsValue => Method.Signature!.ReturnsValue;
	public override int PopCount => ParameterCount + (IsStatic ? 0 : 1);
	public override int PushCount => ReturnsValue ? 1 : 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(IsStatic ? CilOpCodes.Call : CilOpCodes.Callvirt, Method);
	}
}
