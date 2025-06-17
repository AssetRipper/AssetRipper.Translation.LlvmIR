using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class BitCastInstructionContext : InstructionContext
{
	internal BitCastInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMBitCast);
		Debug.Assert(Operands.Length == 1);

		Debug.Assert(ResultTypeSignature is not PointerTypeSignature);
		Debug.Assert(SourceTypeSignature is not PointerTypeSignature);
	}

	public LLVMValueRef SourceOperand => Operands[0];
	public TypeSignature SourceTypeSignature => Module.GetTypeSignature(SourceOperand.TypeOf);

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Module.LoadValue(instructions, SourceOperand);

		IMethodDescriptor method = Module.InstructionHelperType.Methods
			.First(m => m.Name == nameof(InstructionHelper.BitCast))
			.MakeGenericInstanceMethod(SourceTypeSignature, ResultTypeSignature);

		instructions.Add(CilOpCodes.Call, method);
	}
}
