using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace AssetRipper.Translation.LlvmIR.Variables;

public abstract class ConstantVariable(TypeSignature type) : IVariable
{
	public TypeSignature VariableType { get; } = type;
	public abstract bool IsDefault { get; }
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

	public static ConstantVariable CreateDefault(TypeSignature type)
	{
		return (type as CorLibTypeSignature)?.ElementType switch
		{
			ElementType.Boolean or ElementType.Char or ElementType.I1 or ElementType.U1 or ElementType.I2 or ElementType.U2 or ElementType.I4 or ElementType.U4 => new ConstantI4(0, type.ContextModule!),
			ElementType.I8 or ElementType.U8 => new ConstantI8(0, type.ContextModule!),
			ElementType.R4 => new ConstantR4(0.0f, type.ContextModule!),
			ElementType.R8 => new ConstantR8(0.0, type.ContextModule!),
			_ => new DefaultVariable(type),
		};
	}
}
