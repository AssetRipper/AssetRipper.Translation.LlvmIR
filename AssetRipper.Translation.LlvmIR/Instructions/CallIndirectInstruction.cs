using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class CallIndirectInstruction(StandAloneSignature Signature) : Instruction
{
	public MethodSignature MethodSignature => (MethodSignature)Signature.Signature!;
	public override int PopCount => MethodSignature.ParameterTypes.Count + 1; // +1 for the function pointer
	public override int PushCount => MethodSignature.ReturnsValue ? 1 : 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Calli, Signature);
	}

	public CallIndirectInstruction(MethodSignature methodSignature)
		: this(new StandAloneSignature(methodSignature))
	{
	}

	public CallIndirectInstruction(TypeSignature returnType, params IEnumerable<TypeSignature> parameterTypes)
		: this(MethodSignature.CreateStatic(returnType, parameterTypes))
	{
	}
}
