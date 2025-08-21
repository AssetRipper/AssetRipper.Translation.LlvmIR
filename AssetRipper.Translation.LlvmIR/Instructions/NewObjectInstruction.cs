using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class NewObjectInstruction(IMethodDescriptor constructor) : Instruction
{
	public IMethodDescriptor Constructor { get; } = constructor;
	public override int PopCount => Constructor.Signature!.ParameterTypes.Count;
	public override int PushCount => 1;

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Newobj, Constructor);
	}
}
