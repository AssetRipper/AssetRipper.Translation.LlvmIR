using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AssetRipper.CIL;

namespace AssetRipper.Translation.LlvmIR.Variables;

public sealed class DefaultVariable(TypeSignature type) : ConstantVariable(type)
{
	public override bool IsDefault => true;
	public override void AddLoad(CilInstructionCollection instructions)
	{
		instructions.AddDefaultValue(VariableType);
	}
}
