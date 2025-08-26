using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Variables;

public sealed class ConstantI4(int value, ModuleDefinition module) : ConstantVariable(module.CorLibTypeFactory.Int32)
{
	public int Value { get; } = value;
	public override void AddLoad(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldc_I4, Value);
	}
	public override string ToString()
	{
		return $"ConstantI4 {{ {Value} }}";
	}
}
