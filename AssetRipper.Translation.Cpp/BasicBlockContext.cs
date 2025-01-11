using AssetRipper.Translation.Cpp.Extensions;
using AssetRipper.Translation.Cpp.Instructions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp;

internal sealed class BasicBlockContext
{
	public LLVMBasicBlockRef Block { get; }
	public FunctionContext Function { get; }
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
		BasicBlockContext context = new(block, function);
		foreach (LLVMValueRef instruction in block.GetInstructions())
		{
			InstructionContext instructionContext = InstructionContext.Create(instruction, context, function);
			context.Instructions.Add(instructionContext);
		}
		return context;
	}
}
