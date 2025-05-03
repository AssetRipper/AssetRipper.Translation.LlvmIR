using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.Cpp.ExceptionHandling;

public static class BlockLifter
{
	public static ISeseRegion LiftBasicBlocks(IBasicBlock entrypoint)
	{
		if (!entrypoint.IsFunctionEntrypoint)
		{
			throw new ArgumentException("The entrypoint must be a function entrypoint.", nameof(entrypoint));
		}
		if (entrypoint.AllSuccessors.Count == 0)
		{
			return entrypoint; // No need to lift a single block.
		}

		IReadOnlyList<ISeseRegion> currentRegions = entrypoint.GetThisAndAllSuccessorsRecursively();
		while (TryLift(currentRegions, out ISeseRegion[]? liftedRegions))
		{
			currentRegions = liftedRegions;
		}

		if (currentRegions.Count == 1)
		{
			return currentRegions[0]; // No need to lift a single block.
		}
		else
		{
			return new CompositeSeseRegion(currentRegions);
		}
	}

	private static bool TryLift(IReadOnlyList<ISeseRegion> regions, [NotNullWhen(true)] out ISeseRegion[]? liftedRegions)
	{
		if (TryCombine(regions, out CompositeSeseRegion? compositeRegions))
		{
			liftedRegions = CreateNewLevel(regions, [compositeRegions]);
			return true;
		}
		else
		{
			liftedRegions = null;
			return false;
		}
	}

	private static bool TryCombine(IReadOnlyList<ISeseRegion> regions, [NotNullWhen(true)] out CompositeSeseRegion? composite)
	{
		foreach (ISeseRegion region in regions)
		{
			if (region.TryMatchSequential(out IReadOnlyList<ISeseRegion>? blocks))
			{
				composite = new CompositeSeseRegion(blocks, region.IsExceptionHandlerEntrypoint, blocks.Any(b => b.IsExceptionHandlerExitpoint), false);
				return true;
			}
			else if (region.TryMatchSwitchBlock(out blocks))
			{
				composite = new CompositeSeseRegion(blocks, region.IsExceptionHandlerEntrypoint, false, false);
				return true;
			}
			else if (region.TryMatchProtectedRegionWithExceptionHandlers(out blocks))
			{
				composite = new CompositeSeseRegion(blocks, region.IsExceptionHandlerEntrypoint, region.IsExceptionHandlerExitpoint, false);
				return true;
			}
			else if (region.TryMatchExceptionHandlerBlocks(out blocks))
			{
				composite = new CompositeSeseRegion(blocks, true, true, false);
				return true;
			}
			else if (region.TryMatchProtectedCode(out blocks))
			{
				composite = new CompositeSeseRegion(blocks, region.IsExceptionHandlerEntrypoint, blocks.Any(b => b.IsExceptionHandlerExitpoint), false);
				return true;
			}
		}

		composite = null;
		return false;
	}

	private static ISeseRegion[] CreateNewLevel(IReadOnlyList<ISeseRegion> oldRegions, IEnumerable<CompositeSeseRegion> newCompositeRegion)
	{
		Dictionary<ISeseRegion, ModifiableSeseRegion> oldToNew = new(oldRegions.Count);

		int compositeCount = 0;
		int compositeChildCount = 0;
		int nonLiftedCount = 0;

		foreach (CompositeSeseRegion compositeRegion in newCompositeRegion)
		{
			compositeCount++;
			compositeChildCount += compositeRegion.Children.Count;
			foreach (ISeseRegion child in compositeRegion.Children)
			{
				oldToNew.Add(child, compositeRegion);
			}
		}
		foreach (ISeseRegion region in oldRegions)
		{
			if (!oldToNew.ContainsKey(region))
			{
				nonLiftedCount++;
				LiftedSeseRegion liftedBlock = new(region);
				oldToNew.Add(region, liftedBlock);
			}
		}

		Debug.Assert(compositeChildCount + nonLiftedCount == oldRegions.Count);

		foreach ((ISeseRegion original, ModifiableSeseRegion replacement) in oldToNew)
		{
			foreach (ISeseRegion originalPredecessor in original.AllPredecessors)
			{
				ModifiableSeseRegion newPredecessor = oldToNew[originalPredecessor];
				if (replacement != newPredecessor)
				{
					replacement.AllPredecessors.TryAdd(newPredecessor);
				}
			}
			foreach (ISeseRegion originalPredecessor in original.NormalPredecessors)
			{
				ModifiableSeseRegion newPredecessor = oldToNew[originalPredecessor];
				if (replacement != newPredecessor)
				{
					replacement.NormalPredecessors.TryAdd(newPredecessor);
				}
			}
			foreach (ISeseRegion originalSuccessor in original.AllSuccessors)
			{
				ModifiableSeseRegion newSuccessor = oldToNew[originalSuccessor];
				if (replacement != newSuccessor)
				{
					replacement.AllSuccessors.TryAdd(newSuccessor);
				}
			}
			foreach (ISeseRegion originalSuccessor in original.NormalSuccessors)
			{
				ModifiableSeseRegion newSuccessor = oldToNew[originalSuccessor];
				if (replacement != newSuccessor)
				{
					replacement.NormalSuccessors.TryAdd(newSuccessor);
				}
			}
		}

		int finalCount = nonLiftedCount + compositeCount;
		List<ISeseRegion> list = new(finalCount);
		list.AddRange(oldRegions.Select(b => oldToNew[b]).Distinct());

		return [.. list];
	}
}
