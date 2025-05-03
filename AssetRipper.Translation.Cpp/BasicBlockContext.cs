using AssetRipper.Translation.Cpp.ExceptionHandling;
using AssetRipper.Translation.Cpp.Extensions;
using AssetRipper.Translation.Cpp.Instructions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp;

internal sealed class BasicBlockContext : IBasicBlock
{
	public LLVMBasicBlockRef Block { get; }
	public FunctionContext Function { get; }
	public List<InstructionContext> Instructions { get; } = new();
	public List<BasicBlockContext> Predecessors { get; } = new();
	public List<BasicBlockContext> Successors { get; } = new();
	public List<BasicBlockContext> NormalPredecessors { get; } = new();
	public List<BasicBlockContext> NormalSuccessors { get; } = new();
	public bool StartsWithCatchSwitch => StartsWith(LLVMOpcode.LLVMCatchSwitch);
	public bool StartsWithCatchPad => StartsWith(LLVMOpcode.LLVMCatchPad);
	public bool EndsWithCatchReturn => EndsWith(LLVMOpcode.LLVMCatchRet);
	public bool EndsWithInvoke => EndsWith(LLVMOpcode.LLVMInvoke);
	public bool IsFunctionEntrypoint => Function.BasicBlocks[0] == this;

	private BasicBlockContext(LLVMBasicBlockRef block, FunctionContext function)
	{
		Block = block;
		Function = function;
	}

	public bool StartsWith(LLVMOpcode opcode)
	{
		if (Instructions.Count == 0)
		{
			return false;
		}
		return Instructions[0].Opcode == opcode;
	}

	public bool EndsWith(LLVMOpcode opcode)
	{
		if (Instructions.Count == 0)
		{
			return false;
		}
		return Instructions[^1].Opcode == opcode;
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

	IReadOnlyList<ISeseRegion> ISeseRegion.AllPredecessors => Predecessors;

	IReadOnlyList<ISeseRegion> ISeseRegion.AllSuccessors => Successors;

	IReadOnlyList<ISeseRegion> ISeseRegion.NormalPredecessors => NormalPredecessors;

	IReadOnlyList<ISeseRegion> ISeseRegion.NormalSuccessors => NormalSuccessors;

	bool ISeseRegion.IsExceptionHandlerEntrypoint => StartsWithCatchPad;

	bool ISeseRegion.IsExceptionHandlerExitpoint => EndsWithCatchReturn;

	bool ISeseRegion.IsExceptionHandlerSwitch => StartsWithCatchSwitch;
}
