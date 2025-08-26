using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Variables;

public sealed class ConstantI8(long value, ModuleDefinition module) : ConstantVariable(module.CorLibTypeFactory.Int64)
{
	public long Value { get; } = value;
	public override void AddLoad(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldc_I8, Value);
	}
	public override string ToString()
	{
		return $"ConstantI8 {{ {Value} }}";
	}
}
