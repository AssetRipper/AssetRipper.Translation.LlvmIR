using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Variables;

public sealed class DefaultVariable(TypeSignature type) : ConstantVariable(type)
{
	public override void AddLoad(CilInstructionCollection instructions)
	{
		instructions.AddDefaultValue(VariableType);
	}
}
