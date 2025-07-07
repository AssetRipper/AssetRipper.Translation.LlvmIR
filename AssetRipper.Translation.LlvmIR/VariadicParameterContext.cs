using AsmResolver.DotNet;
using AsmResolver.DotNet.Collections;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR;

internal sealed class VariadicParameterContext : BaseParameterContext
{
	public VariadicParameterContext(Parameter definition, FunctionContext function) : base(definition, function)
	{
		Debug.Assert(function.IsVariadic);
		TypeSignature = function.Module.Definition.DefaultImporter
			.ImportType(typeof(ReadOnlySpan<>))
			.MakeGenericInstanceType(function.Module.Definition.CorLibTypeFactory.IntPtr);
	}

	public override string MangledName => "";

	public override string CleanName => "args";

	public override AttributeWrapper[] Attributes => [];
}
