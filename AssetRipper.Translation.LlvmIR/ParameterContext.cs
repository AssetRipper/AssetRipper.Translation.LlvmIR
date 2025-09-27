using AsmResolver.DotNet;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR;

internal sealed class ParameterContext : BaseParameterContext
{
	public ParameterContext(LLVMValueRef parameter, Parameter definition, FunctionContext function) : base(definition, function)
	{
		Parameter = parameter;
		MangledName = parameter.Name ?? "";
		Attributes = AttributeWrapper.FromArray(function.Function.GetAttributesAtIndex((LLVMAttributeIndex)(Index + 1)));
		if (Index == 0 && function.Function.TryGetStructReturnType(out LLVMTypeRef type))
		{
			CleanName = NameGenerator.CleanName(MangledName, "result");
			StructReturnTypeSignature = Module.GetTypeSignature(type);
			TypeSignature = StructReturnTypeSignature.MakePointerType();
		}
		else
		{
			CleanName = NameGenerator.CleanName(MangledName, "");
			if (CleanName.Length == 0)
			{
				CleanName = $"parameter_{Index}";
			}
			TypeSignature = Module.GetTypeSignature(parameter.TypeOf);
		}
	}

	/// <inheritdoc/>
	public override string MangledName { get; }
	/// <inheritdoc/>
	public override string CleanName { get; }
	public override AttributeWrapper[] Attributes { get; }
	public int Index => Definition.Index;
	public LLVMValueRef Parameter { get; }
	public TypeSignature? StructReturnTypeSignature { get; }
}
