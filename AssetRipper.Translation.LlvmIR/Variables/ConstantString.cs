﻿using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Variables;

public sealed class ConstantString(string value, ModuleDefinition module) : ConstantVariable(module.CorLibTypeFactory.String)
{
	public string Value { get; } = value;
	public override void AddLoad(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldstr, Value);
	}
}
