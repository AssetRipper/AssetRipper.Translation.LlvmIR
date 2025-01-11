using AsmResolver.DotNet.Signatures;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class StoreInstructionContext : InstructionContext
{
	internal StoreInstructionContext(LLVMValueRef instruction, BasicBlockContext block, FunctionContext function) : base(instruction, block, function)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMStore);
		Debug.Assert(Operands.Length == 2);
		Debug.Assert(DestinationOperand.IsInstruction());
		ResultTypeSignature = function.Module.Definition.CorLibTypeFactory.Void;
	}
	public LLVMValueRef SourceOperand => Operands[0];
	public LLVMValueRef DestinationOperand => Operands[1];
	public InstructionContext DestinationInstruction { get; set; } = null!; // Set during Analysis
	public TypeSignature StoreTypeSignature { get; set; } = null!; // Set during Analysis
	public override TypeSignature? SecondaryTypeSignature { get => StoreTypeSignature; set => StoreTypeSignature = value; }
}
