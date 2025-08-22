using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Variables;

namespace AssetRipper.Translation.LlvmIR;

internal abstract class BaseParameterContext : IHasName, IVariable
{
	public abstract string MangledName { get; }
	public string? DemangledName => null;
	public abstract string CleanName { get; }
	public string Name { get; set; } = "";
	public abstract AttributeWrapper[] Attributes { get; }
	public FunctionContext Function { get; }
	public ModuleContext Module => Function.Module;
	public Parameter Definition { get; set; }
	public TypeSignature TypeSignature
	{
		get => Definition.ParameterType;
		protected set => Definition.ParameterType = value;
	}

	public BaseParameterContext(Parameter definition, FunctionContext function)
	{
		Definition = definition;
		Function = function;
	}

	TypeSignature IVariable.VariableType => TypeSignature;

	bool IVariable.SupportsLoadAddress => true;

	void IVariable.AddLoad(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldarg, Definition);
	}

	void IVariable.AddStore(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Starg, Definition);
	}

	void IVariable.AddLoadAddress(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Ldarga, Definition);
	}
}
