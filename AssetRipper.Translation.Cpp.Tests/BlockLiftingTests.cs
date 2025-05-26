using AssetRipper.Translation.Cpp.ExceptionHandling;
using NUnit.Framework;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Tests;

public class BlockLiftingTests
{
	#region Helpers
	private readonly record struct BasicBlockDefinition(
		IEnumerable<int> NormalSuccessors,
		IEnumerable<int> InvokeSuccessors,
		IEnumerable<int> HandlerSuccessors,
		SeseRegionType Type);

	private sealed class BasicBlock(int index) : IBasicBlock
	{
		public List<IBasicBlock> AllPredecessors { get; } = new();
		public List<IBasicBlock> AllSuccessors { get; } = new();
		public List<IBasicBlock> NormalPredecessors { get; } = new();
		public List<IBasicBlock> NormalSuccessors { get; } = new();
		public List<IBasicBlock> InvokePredecessors { get; } = new();
		public List<IBasicBlock> InvokeSuccessors { get; } = new();
		public List<IBasicBlock> HandlerPredecessors { get; } = new();
		public List<IBasicBlock> HandlerSuccessors { get; } = new();
		public SeseRegionType Type { get; set; }
		public bool IsFunctionEntrypoint => Index == 0;
		public int Index { get; } = index;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		IReadOnlyList<ISeseRegion> ISeseRegion.AllPredecessors => AllPredecessors;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		IReadOnlyList<ISeseRegion> ISeseRegion.AllSuccessors => AllSuccessors;

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

		public override string ToString()
		{
			return $"Block {Index}";
		}
	}

	private static BasicBlock[] CreateBlocks(ReadOnlySpan<BasicBlockDefinition> definitions)
	{
		BasicBlock[] blocks = new BasicBlock[definitions.Length];
		for (int i = 0; i < definitions.Length; i++)
		{
			blocks[i] = new BasicBlock(i);
		}
		for (int i = 0; i < definitions.Length; i++)
		{
			BasicBlockDefinition definition = definitions[i];
			BasicBlock block = blocks[i];
			foreach (int successorIndex in definition.NormalSuccessors)
			{
				BasicBlock successor = blocks[successorIndex];
				block.NormalSuccessors.Add(successor);
				successor.NormalPredecessors.Add(block);
				block.AllSuccessors.Add(successor);
				successor.AllPredecessors.Add(block);
			}
			foreach (int successorIndex in definition.InvokeSuccessors)
			{
				BasicBlock successor = blocks[successorIndex];
				block.InvokeSuccessors.Add(successor);
				successor.InvokePredecessors.Add(block);
				block.AllSuccessors.Add(successor);
				blocks[successorIndex].AllPredecessors.Add(block);
			}
			foreach (int successorIndex in definition.HandlerSuccessors)
			{
				BasicBlock successor = blocks[successorIndex];
				block.HandlerSuccessors.Add(successor);
				successor.HandlerPredecessors.Add(block);
				block.AllSuccessors.Add(successor);
				successor.AllPredecessors.Add(block);
			}
			block.Type = definition.Type;
		}
		return blocks;
	}

	private static IReadOnlyList<ISeseRegion> Lift(params ReadOnlySpan<BasicBlockDefinition> definitions)
	{
		BasicBlock[] blocks = CreateBlocks(definitions);
		BasicBlock entrypoint = blocks[0];
		return BlockLifter.LiftBasicBlocks(entrypoint);
	}
	#endregion

	[Test]
	public void SingleBlockIsNotLifted()
	{
		IReadOnlyList<ISeseRegion> lifted = Lift(new BasicBlockDefinition([], [], [], SeseRegionType.None));
		Assert.That(lifted, Has.Count.EqualTo(1));
		Assert.That(lifted[0] is IBasicBlock);
	}

	[Test]
	public void SimpleLiftWithoutExceptionHandling()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None),
			new BasicBlockDefinition([], [], [], SeseRegionType.None),
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		Assert.That(lifted, Has.Count.EqualTo(1));
		Assert.That(lifted[0] is ICompositeSeseRegion);
	}

	[Test]
	public void ExceptionHandlingInsideForLoop()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None),
			new BasicBlockDefinition([4], [2], [], SeseRegionType.None),
			new BasicBlockDefinition([], [], [3], SeseRegionType.ExceptionHandlerSwitch), // Switch
			new BasicBlockDefinition([], [], [4], SeseRegionType.ExceptionHandlerEntrypoint | SeseRegionType.ExceptionHandlerExitpoint), // Self contained handler
			new BasicBlockDefinition([5, 7], [], [], SeseRegionType.None), // Leave target
			new BasicBlockDefinition([6], [], [], SeseRegionType.None),
			new BasicBlockDefinition([1], [], [], SeseRegionType.None),
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // Return
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingOverEntireForLoop()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None),
			new BasicBlockDefinition([4], [2], [], SeseRegionType.None),
			new BasicBlockDefinition([], [], [3], SeseRegionType.ExceptionHandlerSwitch), // Switch
			new BasicBlockDefinition([], [], [7], SeseRegionType.ExceptionHandlerEntrypoint | SeseRegionType.ExceptionHandlerExitpoint), // Self contained handler
			new BasicBlockDefinition([5, 7], [], [], SeseRegionType.None),
			new BasicBlockDefinition([6], [], [], SeseRegionType.None),
			new BasicBlockDefinition([1], [], [], SeseRegionType.None),
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // Leave target and return
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithMultipleInvocations()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None),
			new BasicBlockDefinition([2], [4], [], SeseRegionType.None),
			new BasicBlockDefinition([3], [4], [], SeseRegionType.None),
			new BasicBlockDefinition([6], [], [], SeseRegionType.None),
			new BasicBlockDefinition([], [], [5], SeseRegionType.ExceptionHandlerSwitch), // Switch
			new BasicBlockDefinition([], [], [6], SeseRegionType.ExceptionHandlerEntrypoint | SeseRegionType.ExceptionHandlerExitpoint), // Self contained handler
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // Leave target and return
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithNestedTryCatch1()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([2], [6], [], SeseRegionType.None), // 1, caught by outer switch
			new BasicBlockDefinition([3], [7], [], SeseRegionType.None), // 2, caught by inner switch
			new BasicBlockDefinition([4], [], [], SeseRegionType.None), // 3
			new BasicBlockDefinition([5], [], [], SeseRegionType.None), // 4 Leave target
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 5 Return
			new BasicBlockDefinition([], [], [10], SeseRegionType.ExceptionHandlerSwitch), // 6 Outer switch
			new BasicBlockDefinition([], [], [8], SeseRegionType.ExceptionHandlerSwitch), // 7 Inner switch
			new BasicBlockDefinition([9], [6], [], SeseRegionType.ExceptionHandlerEntrypoint), // 8 Inner handler entry, caught by outer switch
			new BasicBlockDefinition([], [], [4], SeseRegionType.ExceptionHandlerExitpoint), // 9 Inner handler exit
			new BasicBlockDefinition([11], [], [], SeseRegionType.ExceptionHandlerEntrypoint), // 10 Outer handler entry
			new BasicBlockDefinition([12], [], [], SeseRegionType.None), // 11 Outer handler middle
			new BasicBlockDefinition([], [], [4], SeseRegionType.ExceptionHandlerExitpoint), // 12 Outer handler exit
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithNestedTryCatch2()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([2], [6], [], SeseRegionType.None), // 1, caught by outer switch
			new BasicBlockDefinition([3], [7], [], SeseRegionType.None), // 2, caught by inner switch
			new BasicBlockDefinition([4], [], [], SeseRegionType.None), // 3
			new BasicBlockDefinition([5], [], [], SeseRegionType.None), // 4 Leave target
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 5 Return
			new BasicBlockDefinition([], [], [11], SeseRegionType.ExceptionHandlerSwitch), // 6 Outer switch
			new BasicBlockDefinition([], [], [8], SeseRegionType.ExceptionHandlerSwitch), // 7 Inner switch
			new BasicBlockDefinition([9], [], [], SeseRegionType.ExceptionHandlerEntrypoint), // 8 Inner handler entry
			new BasicBlockDefinition([10], [6], [], SeseRegionType.None), // 9 Inner handler middle, caught by outer switch
			new BasicBlockDefinition([], [], [4], SeseRegionType.ExceptionHandlerExitpoint), // 10 Inner handler exit
			new BasicBlockDefinition([12], [], [], SeseRegionType.ExceptionHandlerEntrypoint), // 11 Outer handler entry
			new BasicBlockDefinition([13], [], [], SeseRegionType.None), // 12 Outer handler middle
			new BasicBlockDefinition([], [], [4], SeseRegionType.ExceptionHandlerExitpoint), // 13 Outer handler exit
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithBranching()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([2, 3], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([6], [4], [], SeseRegionType.None), // 2
			new BasicBlockDefinition([6], [4], [], SeseRegionType.None), // 3
			new BasicBlockDefinition([], [], [5], SeseRegionType.ExceptionHandlerSwitch), // 4 Switch
			new BasicBlockDefinition([], [], [6], SeseRegionType.ExceptionHandlerEntrypoint | SeseRegionType.ExceptionHandlerExitpoint), // 5 Self contained handler
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 6 Leave target and return
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithUnreachableCode()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [2], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 1 Block containing an unreachable instruction
			new BasicBlockDefinition([], [], [3], SeseRegionType.ExceptionHandlerSwitch), // 2 Switch
			new BasicBlockDefinition([], [], [4], SeseRegionType.ExceptionHandlerEntrypoint | SeseRegionType.ExceptionHandlerExitpoint), // 3 Self contained handler
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 4 Leave target and return
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithExtraBlock()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([5], [2], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([], [], [3], SeseRegionType.ExceptionHandlerSwitch), // 2 Switch
			new BasicBlockDefinition([], [], [4], SeseRegionType.ExceptionHandlerEntrypoint | SeseRegionType.ExceptionHandlerExitpoint), // 3 Self contained handler
			new BasicBlockDefinition([6], [], [], SeseRegionType.None), // 4 The extra block
			new BasicBlockDefinition([6], [], [], SeseRegionType.None), // 5
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 6
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void SimpleCleanup()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [2], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([4], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([3], [], [], SeseRegionType.CleanupEntrypoint), // 2 Self contained handler
			new BasicBlockDefinition([], [], [], SeseRegionType.CleanupExitpoint), // 3 Unwind to caller
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 4
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void IfControlFlow()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1, 2], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([2], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 2
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		Assert.That(lifted, Has.Count.EqualTo(1));
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void GotoControlFlow()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1, 2], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([5], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([3, 4], [], [], SeseRegionType.None), // 2
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 3
			new BasicBlockDefinition([5], [], [], SeseRegionType.None), // 4
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 5
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		Assert.That(lifted, Has.Count.EqualTo(1));
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void DoWhileLoopThatEventuallyEnds()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([2], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([1, 3], [], [], SeseRegionType.None), // 2
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 3
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		Assert.That(lifted, Has.Count.EqualTo(1));
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
		AssertThatHasCompositeContainingIndices(lifted, [0, 1, 2, 3]);
	}

	[Test]
	public void WhileLoopThatEventuallyEnds()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([2, 3], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 2
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 3
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		Assert.That(lifted, Has.Count.EqualTo(1));
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
		AssertThatHasCompositeContainingIndices(lifted, [0, 1, 2, 3]);
	}

	[Test]
	public void WhileLoopThatDoesNotEnd()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([2], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([1], [], [], SeseRegionType.None), // 2
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		Assert.That(lifted, Has.Count.EqualTo(1));
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
		AssertThatHasCompositeContainingIndices(lifted, [0, 1, 2]);
	}

	[Test]
	public void WhileLoopAtBeginningOfFunction()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1, 2], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([0], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 2
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
		AssertThatHasCompositeContainingIndices(lifted, [0, 1]);
	}

	[Test]
	public void SwitchWithSharedTarget()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1, 2], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([3], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([3], [], [], SeseRegionType.None), // 2
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 3
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
		AssertThatHasCompositeContainingIndices(lifted, [1, 2]);
	}

	[Test]
	public void SwitchWithNoTarget()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1, 2], [], [], SeseRegionType.None), // 0
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 1
			new BasicBlockDefinition([], [], [], SeseRegionType.None), // 2
		];
		IReadOnlyList<ISeseRegion> lifted = Lift(definitions);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
		AssertThatHasCompositeContainingIndices(lifted, [1, 2]);
	}

	private static void AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(IReadOnlyList<ISeseRegion> lifted)
	{
		using (Assert.EnterMultipleScope())
		{
			Assert.That(lifted.Any(c => c is ICompositeSeseRegion));
			Assert.That(lifted.All(c => c.Type == SeseRegionType.None));
		}
	}

	private static void AssertThatEntrypointIsFirstAtAllLevels(IReadOnlyList<ISeseRegion> lifted)
	{
		ISeseRegion current = lifted[0];
		while (current is not IBasicBlock)
		{
			Assert.That(current.IsFunctionEntrypoint);
			current = current switch
			{
				ICompositeSeseRegion composite => composite.Children[0],
				ISeseRegionAlias alias => alias.Original,
				_ => throw new NotSupportedException(),
			};
		}
	}

	private static void AssertThatHasCompositeContainingIndices(IReadOnlyList<ISeseRegion> lifted, params ReadOnlySpan<int> indices)
	{
		bool passed = false;
		foreach (ISeseRegion region in lifted)
		{
			HashSet<int> containedIndices = GetContainingIndices(region).ToHashSet();
			if (!containedIndices.Contains(indices[0]))
			{
				// Two regions can never contain the same index, so it's pointless to check all of them.
				continue;
			}

			foreach (int index in indices)
			{
				Assert.That(containedIndices.Contains(index), $"Region {region} does not contain index {index}.");
			}
			passed = true;
		}
		if (!passed)
		{
			Assert.Fail($"No region contains indices {string.Join(", ", indices.ToArray())}.");
		}
	}

	private static IEnumerable<int> GetContainingIndices(ISeseRegion region) => region switch
	{
		ICompositeSeseRegion composite => composite.Children.SelectMany(GetContainingIndices),
		ISeseRegionAlias alias => GetContainingIndices(alias.Original),
		BasicBlock block => [block.Index],
		_ => throw new NotSupportedException($"Unsupported region type: {region.GetType().Name}"),
	};
}
