using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Variables;

public class LocalVariable(TypeSignature variableType) : IVariable
{
	public bool IsTemporary => true;
	public TypeSignature VariableType { get; } = variableType;
	private CilLocalVariable? Variable { get; set; }

	public void AddLoad(CilInstructionCollection instructions)
	{
		Variable ??= instructions.AddLocalVariable(VariableType);
		instructions.Add(CilOpCodes.Ldloc, Variable);
	}

	public void AddStore(CilInstructionCollection instructions)
	{
		Variable ??= instructions.AddLocalVariable(VariableType);
		instructions.Add(CilOpCodes.Stloc, Variable);
	}

	public void AddLoadAddress(CilInstructionCollection instructions)
	{
		Variable ??= instructions.AddLocalVariable(VariableType);
		instructions.Add(CilOpCodes.Ldloca, Variable);
	}

	public override string ToString() => $"Local {{ {VariableType} }}";
}
