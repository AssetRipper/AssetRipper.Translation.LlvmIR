using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.LlvmIR.Variables;

public class LocalVariable(TypeSignature variableType) : IVariable
{
	public bool IsTemporary => true;
	public TypeSignature VariableType { get; } = variableType;
	private CilLocalVariable? Variable { get; set; }

	public void AddLoad(CilInstructionCollection instructions)
	{
		EnsureVariableCreated(instructions);
		instructions.Add(CilOpCodes.Ldloc, Variable);
	}

	public void AddStore(CilInstructionCollection instructions)
	{
		EnsureVariableCreated(instructions);
		instructions.Add(CilOpCodes.Stloc, Variable);
	}

	public void AddLoadAddress(CilInstructionCollection instructions)
	{
		EnsureVariableCreated(instructions);
		instructions.Add(CilOpCodes.Ldloca, Variable);
	}

	public void AddStoreDefault(CilInstructionCollection instructions)
	{
		EnsureVariableCreated(instructions);
		instructions.InitializeDefaultValue(Variable);
	}

	[MemberNotNull(nameof(Variable))]
	private void EnsureVariableCreated(CilInstructionCollection instructions)
	{
		Variable ??= instructions.AddLocalVariable(VariableType);
	}

	public override string ToString() => $"Local {{ {VariableType} }}";
}
