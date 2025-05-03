using AssetRipper.Translation.Cpp.ExceptionHandling;
using NUnit.Framework;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Tests;

public class BlockLiftingTests
{
	#region Helpers
	private readonly record struct BasicBlockDefinition(
		IEnumerable<int> NormalSuccessors,
		IEnumerable<int> AbnormalSuccessors,
		bool IsExceptionHandlerEntrypoint,
		bool IsExceptionHandlerExitpoint,
		bool IsExceptionHandlerSwitch);

	private sealed class BasicBlock(int index) : IBasicBlock
	{
		public List<IBasicBlock> AllPredecessors { get; } = new();
		public List<IBasicBlock> AllSuccessors { get; } = new();
		public List<IBasicBlock> NormalPredecessors { get; } = new();
		public List<IBasicBlock> NormalSuccessors { get; } = new();
		public bool IsExceptionHandlerEntrypoint { get; set; }
		public bool IsExceptionHandlerExitpoint { get; set; }
		public bool IsExceptionHandlerSwitch { get; set; }
		public bool IsFunctionEntrypoint => index == 0;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		IReadOnlyList<ISeseRegion> ISeseRegion.AllPredecessors => AllPredecessors;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		IReadOnlyList<ISeseRegion> ISeseRegion.AllSuccessors => AllSuccessors;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		IReadOnlyList<ISeseRegion> ISeseRegion.NormalPredecessors => NormalPredecessors;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		IReadOnlyList<ISeseRegion> ISeseRegion.NormalSuccessors => NormalSuccessors;

		public override string ToString()
		{
			return $"Block {index}";
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
			foreach (int successorIndex in definition.AbnormalSuccessors)
			{
				BasicBlock successor = blocks[successorIndex];
				block.AllSuccessors.Add(successor);
				blocks[successorIndex].AllPredecessors.Add(block);
			}
			block.IsExceptionHandlerEntrypoint = definition.IsExceptionHandlerEntrypoint;
			block.IsExceptionHandlerExitpoint = definition.IsExceptionHandlerExitpoint;
			block.IsExceptionHandlerSwitch = definition.IsExceptionHandlerSwitch;
		}
		return blocks;
	}

	private static ISeseRegion Lift(params ReadOnlySpan<BasicBlockDefinition> definitions)
	{
		BasicBlock[] blocks = CreateBlocks(definitions);
		BasicBlock entrypoint = blocks[0];
		return BlockLifter.LiftBasicBlocks(entrypoint);
	}
	#endregion

	[Test]
	public void SingleBlockIsNotLifted()
	{
		ISeseRegion lifted = Lift(new BasicBlockDefinition([], [], false, false, false));
		Assert.That(lifted is IBasicBlock);
	}

	[Test]
	public void SimpleLiftWithoutExceptionHandling()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], false, false, false),
			new BasicBlockDefinition([], [], false, false, false),
		];
		ISeseRegion lifted = Lift(definitions);
		Assert.That(lifted is ICompositeSeseRegion);
	}

	[Test]
	public void ExceptionHandlingInsideForLoop()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], false, false, false),
			new BasicBlockDefinition([4], [2], false, false, false),
			new BasicBlockDefinition([], [3], false, false, true), // Switch
			new BasicBlockDefinition([], [4], true, true, false), // Self contained handler
			new BasicBlockDefinition([5, 7], [], false, false, false), // Leave target
			new BasicBlockDefinition([6], [], false, false, false),
			new BasicBlockDefinition([1], [], false, false, false),
			new BasicBlockDefinition([], [], false, false, false), // Return
		];
		ICompositeSeseRegion lifted = (ICompositeSeseRegion)Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingOverEntireForLoop()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], false, false, false),
			new BasicBlockDefinition([4], [2], false, false, false),
			new BasicBlockDefinition([], [3], false, false, true), // Switch
			new BasicBlockDefinition([], [7], true, true, false), // Self contained handler
			new BasicBlockDefinition([5, 7], [], false, false, false),
			new BasicBlockDefinition([6], [], false, false, false),
			new BasicBlockDefinition([1], [], false, false, false),
			new BasicBlockDefinition([], [], false, false, false), // Leave target and return
		];
		ICompositeSeseRegion lifted = (ICompositeSeseRegion)Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithMultipleInvocations()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], false, false, false),
			new BasicBlockDefinition([2], [4], false, false, false),
			new BasicBlockDefinition([3], [4], false, false, false),
			new BasicBlockDefinition([6], [], false, false, false),
			new BasicBlockDefinition([], [5], false, false, true), // Switch
			new BasicBlockDefinition([], [6], true, true, false), // Self contained handler
			new BasicBlockDefinition([], [], false, false, false), // Leave target and return
		];
		ICompositeSeseRegion lifted = (ICompositeSeseRegion)Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithNestedTryCatch1()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], false, false, false), // 0
			new BasicBlockDefinition([2], [6], false, false, false), // 1, caught by outer switch
			new BasicBlockDefinition([3], [7], false, false, false), // 2, caught by inner switch
			new BasicBlockDefinition([4], [], false, false, false), // 3
			new BasicBlockDefinition([5], [], false, false, false), // 4 Leave target
			new BasicBlockDefinition([], [], false, false, false), // 5 Return
			new BasicBlockDefinition([], [10], false, false, true), // 6 Outer switch
			new BasicBlockDefinition([], [8], false, false, true), // 7 Inner switch
			new BasicBlockDefinition([9], [6], true, false, false), // 8 Inner handler entry, caught by outer switch
			new BasicBlockDefinition([], [4], false, true, false), // 9 Inner handler exit
			new BasicBlockDefinition([11], [], true, false, false), // 10 Outer handler entry
			new BasicBlockDefinition([12], [], false, false, false), // 11 Outer handler middle
			new BasicBlockDefinition([], [4], false, true, false), // 12 Outer handler exit
		];
		ICompositeSeseRegion lifted = (ICompositeSeseRegion)Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithNestedTryCatch2()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], false, false, false), // 0
			new BasicBlockDefinition([2], [6], false, false, false), // 1, caught by outer switch
			new BasicBlockDefinition([3], [7], false, false, false), // 2, caught by inner switch
			new BasicBlockDefinition([4], [], false, false, false), // 3
			new BasicBlockDefinition([5], [], false, false, false), // 4 Leave target
			new BasicBlockDefinition([], [], false, false, false), // 5 Return
			new BasicBlockDefinition([], [11], false, false, true), // 6 Outer switch
			new BasicBlockDefinition([], [8], false, false, true), // 7 Inner switch
			new BasicBlockDefinition([9], [], true, false, false), // 8 Inner handler entry
			new BasicBlockDefinition([10], [6], false, false, false), // 9 Inner handler middle, caught by outer switch
			new BasicBlockDefinition([], [4], false, true, false), // 10 Inner handler exit
			new BasicBlockDefinition([12], [], true, false, false), // 11 Outer handler entry
			new BasicBlockDefinition([13], [], false, false, false), // 12 Outer handler middle
			new BasicBlockDefinition([], [4], false, true, false), // 13 Outer handler exit
		];
		ICompositeSeseRegion lifted = (ICompositeSeseRegion)Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithBranching()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [], false, false, false), // 0
			new BasicBlockDefinition([2, 3], [], false, false, false), // 1
			new BasicBlockDefinition([6], [4], false, false, false), // 2
			new BasicBlockDefinition([6], [4], false, false, false), // 3
			new BasicBlockDefinition([], [5], false, false, true), // 4 Switch
			new BasicBlockDefinition([], [6], true, true, false), // 5 Self contained handler
			new BasicBlockDefinition([], [], false, false, false), // 6 Leave target and return
		];
		ICompositeSeseRegion lifted = (ICompositeSeseRegion)Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	[Test]
	public void ExceptionHandlingWithUnreachableCode()
	{
		ReadOnlySpan<BasicBlockDefinition> definitions =
		[
			new BasicBlockDefinition([1], [2], false, false, false), // 0
			new BasicBlockDefinition([], [], false, false, false), // 1 Block containing an unreachable instruction
			new BasicBlockDefinition([], [3], false, false, true), // 2 Switch
			new BasicBlockDefinition([], [4], true, true, false), // 3 Self contained handler
			new BasicBlockDefinition([], [], false, false, false), // 4 Leave target and return
		];
		ICompositeSeseRegion lifted = (ICompositeSeseRegion)Lift(definitions);
		AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(lifted);
		AssertThatEntrypointIsFirstAtAllLevels(lifted);
	}

	private static void AssertThatAllExceptionHandlerSwitchesHaveBeenLifted(ICompositeSeseRegion lifted)
	{
		using (Assert.EnterMultipleScope())
		{
			Assert.That(lifted.Children.Any(c => c is ICompositeSeseRegion));
			Assert.That(lifted.Children.All(c => !c.IsExceptionHandlerSwitch));
		}
	}

	private static void AssertThatEntrypointIsFirstAtAllLevels(ICompositeSeseRegion lifted)
	{
		using (Assert.EnterMultipleScope())
		{
			Assert.That(lifted.IsFunctionEntrypoint);
			Assert.That(lifted.Children[0].IsFunctionEntrypoint);
		}
		ISeseRegion current = lifted.Children[0];
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
}
