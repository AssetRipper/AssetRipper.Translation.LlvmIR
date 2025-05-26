using AssetRipper.Translation.Cpp.ExceptionHandling;
using AssetRipper.Translation.Cpp.Extensions;
using AssetRipper.Translation.Cpp.Instructions;
using LLVMSharp.Interop;
using System.Diagnostics;

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
	public List<BasicBlockContext> InvokePredecessors { get; } = new();
	public List<BasicBlockContext> InvokeSuccessors { get; } = new();
	public List<BasicBlockContext> HandlerPredecessors { get; } = new();
	public List<BasicBlockContext> HandlerSuccessors { get; } = new();
	public bool StartsWithCatchSwitch => StartsWith(LLVMOpcode.LLVMCatchSwitch);
	public bool EndsWithCatchSwitch => EndsWith(LLVMOpcode.LLVMCatchSwitch);
	public bool IsCatchSwitch => StartsWithCatchSwitch && EndsWithCatchSwitch;
	public bool StartsWithCatchPad => StartsWith(LLVMOpcode.LLVMCatchPad);
	public bool EndsWithCatchReturn => EndsWith(LLVMOpcode.LLVMCatchRet);
	public bool StartsWithCleanupPad => StartsWith(LLVMOpcode.LLVMCleanupPad);
	public bool EndsWithCleanupReturn => EndsWith(LLVMOpcode.LLVMCleanupRet);
	public bool EndsWithInvoke => EndsWith(LLVMOpcode.LLVMInvoke);
	public bool EndsWithUnreachable => EndsWith(LLVMOpcode.LLVMUnreachable);
	public bool IsFunctionEntrypoint => Function.BasicBlocks[0] == this;

	private BasicBlockContext(LLVMBasicBlockRef block, FunctionContext function)
	{
		Block = block;
		Function = function;
	}

	public bool StartsWith(LLVMOpcode opcode)
	{
		for (int i = 0; i < Instructions.Count; i++)
		{
			LLVMOpcode instructionOpcode = Instructions[i].Opcode;
			if (instructionOpcode == opcode)
			{
				return true;
			}
			else if (instructionOpcode == LLVMOpcode.LLVMPHI)
			{
				// Ignore phi instructions at the beginning of the block
				continue;
			}
			else
			{
				break;
			}
		}
		return false;
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

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.AllPredecessors => Predecessors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.AllSuccessors => Successors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.NormalPredecessors => NormalPredecessors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.NormalSuccessors => NormalSuccessors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.InvokePredecessors => InvokePredecessors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.InvokeSuccessors => InvokeSuccessors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.HandlerPredecessors => HandlerPredecessors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.HandlerSuccessors => HandlerSuccessors;

	SeseRegionType ISeseRegion.Type
	{
		get
		{
			SeseRegionType type = SeseRegionType.None;
			if (StartsWithCatchPad)
			{
				type |= SeseRegionType.ExceptionHandlerEntrypoint;
			}
			if (EndsWithCatchReturn)
			{
				type |= SeseRegionType.ExceptionHandlerExitpoint;
			}
			if (IsCatchSwitch)
			{
				type |= SeseRegionType.ExceptionHandlerSwitch;
			}
			if (StartsWithCleanupPad)
			{
				type |= SeseRegionType.CleanupEntrypoint;
			}
			if (EndsWithCleanupReturn || EndsWithUnreachable)
			{
				type |= SeseRegionType.CleanupExitpoint;
			}
			return type;
		}
	}
}
