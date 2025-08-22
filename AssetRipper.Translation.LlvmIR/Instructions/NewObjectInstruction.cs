using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class NewObjectInstruction(IMethodDescriptor Constructor) : Instruction
{
	public override int PopCount => Constructor.Signature!.ParameterTypes.Count;
	public override int PushCount => 1;

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Newobj, Constructor);
	}

	public bool Equals(NewObjectInstruction? other)
	{
		if (other is null)
		{
			return false;
		}
		return SignatureComparer.Default.Equals(Constructor, other.Constructor);
	}

	public override int GetHashCode()
	{
		return SignatureComparer.Default.GetHashCode(Constructor);
	}
}
