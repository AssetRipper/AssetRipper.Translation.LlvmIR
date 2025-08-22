using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Variables;

internal class FunctionPointerVariable(FunctionContext function) : IVariable
{
	public TypeSignature VariableType => Function.Module.Definition.CorLibTypeFactory.Void.MakePointerType();
	public bool SupportsStore => false;
	public FunctionContext Function { get; } = function;
	public void AddLoad(CilInstructionCollection instructions)
	{
		Function.AddLoadFunctionPointer(instructions);
	}
	void IVariable.AddStore(CilInstructionCollection instructions)
	{
		throw new NotSupportedException();
	}
}
