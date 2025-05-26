using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.Cpp.ExceptionHandling;

public static class BlockLifter
{
	public static IReadOnlyList<ISeseRegion> LiftBasicBlocks(IBasicBlock entrypoint)
	{
		if (!entrypoint.IsFunctionEntrypoint)
		{
			throw new ArgumentException("The entrypoint must be a function entrypoint.", nameof(entrypoint));
		}
		if (entrypoint.AllSuccessors.Count == 0)
		{
			return [entrypoint]; // No need to lift a single block.
		}

		IReadOnlyList<ISeseRegion> currentRegions = entrypoint.GetThisAndAllSuccessorsRecursively();
		while (TryLift(currentRegions, out ISeseRegion[]? liftedRegions))
		{
			currentRegions = liftedRegions;
		}

		return currentRegions;
	}

	public static ISeseRegion AsSingleRegion(this IReadOnlyList<ISeseRegion> regions)
	{
		if (regions.Count == 1)
		{
			return regions[0];
		}
		else
		{
			return new CompositeSeseRegion(regions);
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
			if (region.TryMatchSequential(out IReadOnlyList<ISeseRegion>? children))
			{
				composite = new CompositeSeseRegion(
					children,
					region.IsExceptionHandlerEntrypoint,
					children.Any(c => c.IsExceptionHandlerExitpoint),
					false,
					region.IsCleanupEntrypoint,
					children.Any(c => c.IsCleanupExitpoint));
				return true;
			}
			else if (region.TryMatchSwitchBlock(out children))
			{
				composite = new CompositeSeseRegion(
					children,
					region.IsExceptionHandlerEntrypoint,
					false,
					false,
					region.IsCleanupEntrypoint,
					false);
				return true;
			}
			else if (region.TryMatchDoWhileLoop(out children))
			{
				composite = new CompositeSeseRegion(
					children,
					region.IsExceptionHandlerEntrypoint,
					children.Any(c => c.IsExceptionHandlerExitpoint),
					false,
					region.IsCleanupEntrypoint,
					children.Any(c => c.IsCleanupExitpoint));
				return true;
			}
			else if (region.TryMatchWhileLoop(out children))
			{
				composite = new CompositeSeseRegion(
					children,
					region.IsExceptionHandlerEntrypoint,
					children.Any(c => c.IsExceptionHandlerExitpoint),
					false,
					region.IsCleanupEntrypoint,
					children.Any(c => c.IsCleanupExitpoint));
				return true;
			}
			else if (region.TryMatchReverseSequential(out children))
			{
				composite = new CompositeSeseRegion(
					children,
					region.IsExceptionHandlerEntrypoint,
					children.Any(c => c.IsExceptionHandlerExitpoint),
					false,
					region.IsCleanupEntrypoint,
					children.Any(c => c.IsCleanupExitpoint));
				return true;
			}
			else if (region.TryMatchProtectedRegionWithExceptionHandlers(out children))
			{
				composite = new ProtectedRegionWithExceptionHandlers(region, children);
				return true;
			}
			else if (region.TryMatchSelfContainedExceptionHandler(out children))
			{
				composite = new CompositeSeseRegion(children, true, true, false, false, false);
				return true;
			}
			else if (region.TryMatchExceptionHandlerSwitch(out children))
			{
				// This is no longer an exception handler switch, but instead a self-contained exception handler.
				composite = new CompositeSeseRegion(children, true, true, false, false, false);
				return true;
			}
			else if (region.TryMatchSelfContainedCleanup(out children))
			{
				composite = new CompositeSeseRegion(children, false, false, false, true, true);
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
			// This assumes that:
			// * Invoke and handlers will never intersect.
			// * Normal is preferred over invoke and handlers.

			foreach (ISeseRegion originalSuccessor in original.NormalSuccessors)
			{
				ModifiableSeseRegion newSuccessor = oldToNew[originalSuccessor];
				if (replacement != newSuccessor)
				{
					replacement.NormalSuccessors.TryAdd(newSuccessor);
					newSuccessor.NormalPredecessors.TryAdd(replacement);
				}
			}
			foreach (ISeseRegion originalInvokeSuccessor in original.InvokeSuccessors)
			{
				ModifiableSeseRegion newSuccessor = oldToNew[originalInvokeSuccessor];
				if (replacement != newSuccessor)
				{
					replacement.InvokeSuccessors.TryAdd(newSuccessor);
					newSuccessor.InvokePredecessors.TryAdd(replacement);
				}
			}
			if (replacement is ProtectedRegionWithExceptionHandlers composite && composite.ProtectedRegion != original)
			{
				// In this case, exception handling is contained within this composite region,
				// so we treat handler successors as normal successors.
				foreach (ISeseRegion originalHandlerSuccessor in original.HandlerSuccessors)
				{
					ModifiableSeseRegion newSuccessor = oldToNew[originalHandlerSuccessor];
					if (replacement != newSuccessor)
					{
						replacement.NormalSuccessors.TryAdd(newSuccessor);
						newSuccessor.NormalPredecessors.TryAdd(replacement);
					}
				}
			}
			else
			{
				foreach (ISeseRegion originalHandlerSuccessor in original.HandlerSuccessors)
				{
					ModifiableSeseRegion newSuccessor = oldToNew[originalHandlerSuccessor];
					if (replacement != newSuccessor)
					{
						replacement.HandlerSuccessors.TryAdd(newSuccessor);
						newSuccessor.HandlerPredecessors.TryAdd(replacement);
					}
				}
			}

			Debug.Assert(replacement.InvokeSuccessors.Count is 0 or 1);
		}

		int finalCount = nonLiftedCount + compositeCount;
		List<ISeseRegion> list = new(finalCount);
		list.AddRange(oldRegions.Select(b => oldToNew[b]).Distinct());

		return [.. list];
	}
}
