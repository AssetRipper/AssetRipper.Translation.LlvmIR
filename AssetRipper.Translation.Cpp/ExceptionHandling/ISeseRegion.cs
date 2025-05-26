using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.Cpp.ExceptionHandling;

public interface ISeseRegion
{
	IReadOnlyList<ISeseRegion> AllPredecessors { get; }
	IReadOnlyList<ISeseRegion> AllSuccessors { get; }
	IReadOnlyList<ISeseRegion> NormalPredecessors { get; }
	IReadOnlyList<ISeseRegion> NormalSuccessors { get; }
	IReadOnlyList<ISeseRegion> InvokePredecessors { get; }
	IReadOnlyList<ISeseRegion> InvokeSuccessors { get; }
	IReadOnlyList<ISeseRegion> HandlerPredecessors { get; }
	IReadOnlyList<ISeseRegion> HandlerSuccessors { get; }
	SeseRegionType Type { get; }
	public sealed bool IsExceptionHandlerEntrypoint => Type.HasFlag(SeseRegionType.ExceptionHandlerEntrypoint);
	public sealed bool IsExceptionHandlerExitpoint => Type.HasFlag(SeseRegionType.ExceptionHandlerExitpoint);
	public sealed bool IsExceptionHandlerSwitch => Type.HasFlag(SeseRegionType.ExceptionHandlerSwitch);
	public sealed bool IsCleanupEntrypoint => Type.HasFlag(SeseRegionType.CleanupEntrypoint);
	public sealed bool IsCleanupExitpoint => Type.HasFlag(SeseRegionType.CleanupExitpoint);
	bool IsFunctionEntrypoint { get; }
}
internal static class SeseRegionExtensions
{
	extension(ISeseRegion region)
	{
		public IReadOnlyList<ISeseRegion> GetThisAndAllSuccessorsRecursively()
		{
			HashSet<ISeseRegion> set = [region];
			Queue<ISeseRegion> queue = new();
			queue.Enqueue(region);
			while (queue.TryDequeue(out ISeseRegion? current))
			{
				foreach (ISeseRegion successor in current.AllSuccessors)
				{
					if (set.Add(successor))
					{
						queue.Enqueue(successor);
					}
				}
			}
			return [.. set];
		}

		public bool IsNormal => region.Type == SeseRegionType.None;

		public bool IsSelfContainedExceptionHandler => region.IsExceptionHandlerEntrypoint && region.IsExceptionHandlerExitpoint;

		public bool IsSelfContainedCleanup => region.IsCleanupEntrypoint && region.IsCleanupExitpoint;

		public bool HasAbnormalSuccessors => region.AllSuccessors.Count != region.NormalSuccessors.Count;

		public bool HasAbnormalPredecessors => region.AllPredecessors.Count != region.NormalPredecessors.Count;

		public bool HasInvokeSuccessors => region.InvokeSuccessors.Count != 0;

		public bool HasInvokePredecessors => region.InvokePredecessors.Count != 0;

		public bool HasHandlerSuccessors => region.HandlerSuccessors.Count != 0;

		public bool HasHandlerPredecessors => region.HandlerPredecessors.Count != 0;

		/// <summary>
		/// This is invalid and indicates a bug or malformed input.
		/// </summary>
		public bool HasMultipleInvokeTargets => region.InvokeSuccessors.Count > 1;

		public ISeseRegion? InvokeTarget
		{
			get
			{
				return region.InvokeSuccessors.Count is 1 ? region.InvokeSuccessors[0] : null;
			}
		}

		public bool TryMatchSelfContainedExceptionHandler([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			if (!region.IsExceptionHandlerEntrypoint || region.IsExceptionHandlerExitpoint || region.HasAbnormalSuccessors)
			{
				regions = null;
				return false;
			}

			// Principle: all exception handlers must be contiguous and single-entry single-exit.

			HashSet<ISeseRegion> set = [region];
			Queue<ISeseRegion> queue = new();
			queue.Enqueue(region);
			while (queue.TryDequeue(out ISeseRegion? current))
			{
				foreach (ISeseRegion successor in current.NormalSuccessors)
				{
					if (successor.HasInvokeSuccessors)
					{
						// try block within a catch block
						regions = null;
						return false;
					}
					if (successor.HasHandlerSuccessors)
					{
						// Should never happen, except in malformed input.
						regions = null;
						return false;
					}
					if (set.Add(successor) && !successor.IsExceptionHandlerExitpoint)
					{
						queue.Enqueue(successor);
					}
				}
			}
			return LargerThanOne(set, out regions);
		}

		public bool TryMatchSelfContainedCleanup([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			if (!region.IsCleanupEntrypoint || region.IsCleanupExitpoint || region.HasAbnormalSuccessors)
			{
				regions = null;
				return false;
			}

			// Principle: all cleanup must be contiguous and single-entry single-exit.
			// This isn't necessarily true, but it's the assumption we make for now.

			HashSet<ISeseRegion> set = [region];
			Queue<ISeseRegion> queue = new();
			queue.Enqueue(region);
			while (queue.TryDequeue(out ISeseRegion? current))
			{
				foreach (ISeseRegion successor in current.NormalSuccessors)
				{
					if (successor.HasInvokeSuccessors)
					{
						// try block within a catch block
						regions = null;
						return false;
					}
					if (successor.HasHandlerSuccessors)
					{
						// Should never happen, except in malformed input.
						regions = null;
						return false;
					}
					if (set.Add(successor) && !successor.IsCleanupExitpoint)
					{
						queue.Enqueue(successor);
					}
				}
			}
			return LargerThanOne(set, out regions);
		}

		public bool TryMatchExceptionHandlerSwitch([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			// This matches to an exception handler switch and its self-contained exception handlers.
			// The goal is to combine them into a single self-contained exception handler.

			if (region is { AllPredecessors.Count: not 1 })
			{
				return False(out regions);
			}

			if (!region.IsExceptionHandlerSwitch)
			{
				// The invoke target must be an exception handler switch.
				return False(out regions);
			}

			ISeseRegion? parentInvokeTarget = null; // This whole try/catch pair might be enclosed in another try/catch pair.
			foreach (ISeseRegion exceptionHandler in region.AllSuccessors)
			{
				if (!exceptionHandler.IsSelfContainedExceptionHandler)
				{
					return False(out regions);
				}

				ISeseRegion? exceptionHandlerInvokeTarget = exceptionHandler.InvokeTarget;
				if (exceptionHandlerInvokeTarget is null)
				{
				}
				else if (parentInvokeTarget is null)
				{
					parentInvokeTarget = exceptionHandlerInvokeTarget;
				}
				else if (parentInvokeTarget != exceptionHandlerInvokeTarget)
				{
					// Should never happen, except in malformed input.
					return False(out regions);
				}
			}

			regions = [region, .. region.AllSuccessors];
			return true;
		}

		public bool TryMatchProtectedRegionWithExceptionHandlers([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			// Principle: a protected region of code redirects to an exception handler switch which has at least one exception handler.
			// All the exception handlers must be self-contained and exit to the same place as the protected region would have normally exited.

			ISeseRegion? invokeTarget = region.InvokeTarget;
			if (invokeTarget is null or { AllPredecessors.Count: not 1 })
			{
				return False(out regions);
			}

			if (invokeTarget.IsSelfContainedCleanup)
			{
				switch (invokeTarget.AllSuccessors.Count)
				{
					case 0:
						// Unwind to caller
						break;
					case 1:
						// Not implemented: redirecting to an exception handler switch or another cleanup region.
						return False(out regions);
					default:
						// The possibility of this branch depends on other implementation details,
						// like whether a composite region can be formed with two cleanup exit points targeting different places.
						// Regardless, it's a niche extension of case 1.
						return False(out regions);
				}

				regions = [region, invokeTarget];
				return true;
			}

			if (invokeTarget.IsSelfContainedExceptionHandler)
			{
				regions = [region, invokeTarget];
				return true;
			}

			return False(out regions);
		}

		public bool TryMatchSequential([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			if (region.NormalSuccessors.Count != 1)
			{
				// The region must have a single normal successor, but may redirect to an exception handler.
				return False(out regions);
			}

			if (region.HasHandlerSuccessors)
			{
				// The region has an abnormal successor, but it's not an invoke target.
				return False(out regions);
			}

			ISeseRegion normalSuccessor = region.NormalSuccessors[0];
			if (normalSuccessor.AllPredecessors.Count != 1)
			{
				// The successor must only reachable directly from this region.
				return False(out regions);
			}

			Debug.Assert(!region.HasMultipleInvokeTargets);
			Debug.Assert(!normalSuccessor.HasMultipleInvokeTargets);
			if (EqualOrNull(region.InvokeTarget, normalSuccessor.InvokeTarget))
			{
				regions = [region, normalSuccessor];
				return true;
			}
			else
			{
				return False(out regions);
			}
		}

		public bool TryMatchReverseSequential([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			if (region.AllPredecessors.Count != 1 || region.NormalPredecessors.Count != 1)
			{
				// The region must have a single normal predecessor.
				return False(out regions);
			}

			ISeseRegion predecessor = region.NormalPredecessors[0];

			if (predecessor.HasHandlerSuccessors)
			{
				return False(out regions);
			}

			if (!EqualOrNull(region.InvokeTarget, predecessor.InvokeTarget))
			{
				// The invoke target must be the same for both regions.
				return False(out regions);
			}

			regions = [predecessor, region];
			return true;
		}

		public bool TryMatchSwitchBlock([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			// This matches to a traditional switch block, ie where one region has multiple successors that all lead to the same place.

			if (region.NormalSuccessors.Count < 2)
			{
				// The region must have a single normal successor, but may redirect to an exception handler.
				return False(out regions);
			}

			if (region.HasHandlerSuccessors)
			{
				// The region has an abnormal successor, but it's not an invoke target.
				return False(out regions);
			}

			ISeseRegion? invokeTarget = region.InvokeTarget;

			ISeseRegion? ultimateSuccessor = null;
			for (int i = 0; i < region.NormalSuccessors.Count; i++)
			{
				ISeseRegion normalSuccessor = region.NormalSuccessors[i];
				if (normalSuccessor.AllPredecessors.Count != 1)
				{
					// The successor must only reachable directly from this region.
					return False(out regions);
				}
				ISeseRegion? normalSuccessorInvokeTarget = normalSuccessor.InvokeTarget;
				if (normalSuccessorInvokeTarget is null)
				{
				}
				else if (invokeTarget is null)
				{
					invokeTarget = normalSuccessorInvokeTarget;
				}
				else if (normalSuccessorInvokeTarget != invokeTarget)
				{
					// The exception handler switch must be consistent.
					return False(out regions);
				}

				if (normalSuccessor.HasHandlerSuccessors)
				{
					// The successor must not have any abnormal successors, except for the exception handler switch.
					return False(out regions);
				}

				if (normalSuccessor.NormalSuccessors.Count is not 0 and not 1)
				{
					// The successor must have a single normal successor or no successors at all.
					return False(out regions);
				}

				if (i == 0)
				{
					ultimateSuccessor = normalSuccessor.NormalSuccessors.SingleOrDefault();
				}
				else if (normalSuccessor.NormalSuccessors.SingleOrDefault() != ultimateSuccessor)
				{
					// The successor must have the same ultimate successor.
					return False(out regions);
				}
			}

			regions = [region, .. region.NormalSuccessors];
			return true;
		}

		public bool TryMatchDoWhileLoop([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			// A do-while loop is a pair of regions where the first region is the entrypoint
			// and the second region is the exitpoint, but might loop back to the first region instead of exiting.

			if (region.NormalSuccessors.Count != 1 || region.HandlerSuccessors.Count != 0)
			{
				return False(out regions);
			}

			ISeseRegion conditionRegion = region.NormalSuccessors[0];
			if (conditionRegion.AllPredecessors.Count != 1)
			{
				return False(out regions);
			}

			if (!EqualOrNull(region.InvokeTarget, conditionRegion.InvokeTarget))
			{
				// The invoke target must be the same for both regions.
				return False(out regions);
			}

			if (!conditionRegion.NormalSuccessors.Contains(region))
			{
				// The condition region must loop back to the entrypoint.
				return False(out regions);
			}

			regions = [region, conditionRegion];
			return true;
		}

		public bool TryMatchWhileLoop([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			// A while loop is a pair of regions where the first region is both the entrypoint and the exitpoint.
			// The second region is the body of the loop.

			if (region.HandlerSuccessors.Count != 0)
			{
				return False(out regions);
			}

			ISeseRegion? bodyRegion = region.NormalSuccessors.FirstOrDefault(r =>
			{
				return r.AllPredecessors.Count == 1
					&& r.AllPredecessors[0] == region
					&& r.HandlerSuccessors.Count == 0
					&& r.NormalSuccessors.Count == 1
					&& r.NormalSuccessors[0] == region
					&& EqualOrNull(r.InvokeTarget, region.InvokeTarget);
			});

			if (bodyRegion is null)
			{
				return False(out regions);
			}

			regions = [region, bodyRegion];
			return true;
		}
	}

	private static bool False([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
	{
		regions = null;
		return false;
	}

	private static bool LargerThanOne(HashSet<ISeseRegion> set, [NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
	{
		if (set.Count == 1)
		{
			regions = null;
			return false;
		}
		else
		{
			regions = [.. set];
			return true;
		}
	}

	private static bool EqualOrNull(ISeseRegion? a, ISeseRegion? b)
	{
		if (a is null || b is null)
		{
			return true;
		}
		else
		{
			return ReferenceEquals(a, b);
		}
	}
}
