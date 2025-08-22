using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;

namespace AssetRipper.Translation.LlvmIR.Variables;

public abstract class ConstantVariable(TypeSignature type) : IVariable
{
	public TypeSignature VariableType { get; } = type;
	public bool IsTemporary => false; // Constants are not temporary variables; they are fixed values.
	public bool SupportsLoad => true;
	public bool SupportsStore => false;
	public bool SupportsLoadAddress => false;
	public abstract void AddLoad(CilInstructionCollection instructions);
	void IVariable.AddStore(CilInstructionCollection instructions)
	{
		throw new NotSupportedException("Cannot store to a constant variable.");
	}
	void IVariable.AddLoadAddress(CilInstructionCollection instructions)
	{
		throw new NotSupportedException("Cannot load address of a constant variable.");
	}
}
