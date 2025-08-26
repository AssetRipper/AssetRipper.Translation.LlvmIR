using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Variables;

public sealed class ConstantR8(double value, ModuleDefinition module) : ConstantVariable(module.CorLibTypeFactory.Double)
{
	public double Value { get; } = value;
	public override void AddLoad(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldc_R8, Value);
	}
	public override string ToString()
	{
		return $"ConstantR8 {{ {Value} }}";
	}
}
