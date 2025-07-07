using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Instructions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR;

internal sealed class BasicBlockContext
{
	public LLVMBasicBlockRef Block { get; }
	public FunctionContext Function { get; }
	public CilInstructionLabel Label { get; } = new();
	public List<InstructionContext> Instructions { get; } = new();
	public List<BasicBlockContext> Predecessors { get; } = new();
	public List<BasicBlockContext> Successors { get; } = new();

	private BasicBlockContext(LLVMBasicBlockRef block, FunctionContext function)
	{
		Block = block;
		Function = function;
	}

	public static BasicBlockContext Create(LLVMBasicBlockRef block, FunctionContext function)
	{
		BasicBlockContext basicBlock = new(block, function);
		foreach (LLVMValueRef instruction in block.GetInstructions())
		{
			InstructionContext instructionContext = InstructionContext.Create(instruction, function.Module);
			basicBlock.Instructions.Add(instructionContext);
		}
		return basicBlock;
	}

	public override string ToString()
	{
		int index = Function.BasicBlocks.IndexOf(this);
		return $"Block {index}";
	}
}
