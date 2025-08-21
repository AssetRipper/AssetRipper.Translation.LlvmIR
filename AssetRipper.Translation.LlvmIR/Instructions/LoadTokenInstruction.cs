using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class LoadTokenInstruction(IMetadataMember Member) : Instruction
{
	public override int PopCount => 0;
	public override int PushCount => 1;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldtoken, Member);
	}

	public bool Equals(LoadTokenInstruction? other)
	{
		if (other is null)
		{
			return false;
		}
		return Member switch
		{
			IFieldDescriptor { Signature: not null } field => SignatureComparer.Default.Equals(field, other.Member as IFieldDescriptor),
			ITypeDescriptor type => SignatureComparer.Default.Equals(type, other.Member as ITypeDescriptor),
			IMethodDescriptor { Signature: not null } method => SignatureComparer.Default.Equals(method, other.Member as IMethodDescriptor),
			_ => false,
		};
	}
	public override int GetHashCode()
	{
		return Member switch
		{
			IFieldDescriptor { Signature: not null } field => SignatureComparer.Default.GetHashCode(field),
			ITypeDescriptor type => SignatureComparer.Default.GetHashCode(type),
			IMethodDescriptor { Signature: not null } method => SignatureComparer.Default.GetHashCode(method),
			_ => 0,
		};
	}
}
