using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Variables;

public sealed class ConstantR4(float value, ModuleDefinition module) : ConstantVariable(module.CorLibTypeFactory.Single)
{
	public float Value { get; } = value;
	public override void AddLoad(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldc_R4, Value);
	}
	public override string ToString()
	{
		return $"ConstantR4 {{ {Value} }}";
	}
}
