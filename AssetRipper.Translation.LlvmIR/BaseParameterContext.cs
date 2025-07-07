using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;

namespace AssetRipper.Translation.LlvmIR;

internal abstract class BaseParameterContext : IHasName
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
}
