using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.Cpp.ExceptionHandling;

public interface ISeseRegion
{
	IReadOnlyList<ISeseRegion> AllPredecessors { get; }
	IReadOnlyList<ISeseRegion> AllSuccessors { get; }
	IReadOnlyList<ISeseRegion> NormalPredecessors { get; }
	IReadOnlyList<ISeseRegion> NormalSuccessors { get; }
	bool IsExceptionHandlerEntrypoint { get; }
	bool IsExceptionHandlerExitpoint { get; }
	bool IsExceptionHandlerSwitch { get; }
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

		public bool IsNormal => !region.IsExceptionHandlerEntrypoint && !region.IsExceptionHandlerExitpoint && !region.IsExceptionHandlerSwitch;

		public bool IsSelfContainedExceptionHandler => region.IsExceptionHandlerEntrypoint && region.IsExceptionHandlerExitpoint;

		public bool HasAbnormalSuccessors => region.AllSuccessors.Count != region.NormalSuccessors.Count;

		public bool HasAbnormalPredecessors => region.AllPredecessors.Count != region.NormalPredecessors.Count;

		public ISeseRegion? ExceptionHandlerSwitch
		{
			get
			{
				return region.HasAbnormalSuccessors ? region.AllSuccessors.FirstOrDefault(x => x.IsExceptionHandlerSwitch) : null;
			}
		}

		public bool TryMatchProtectedCode([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			// Principle: protected code must be contiguous and single-entry single-exit.

			ISeseRegion? exceptionHandlerSwitch = region.ExceptionHandlerSwitch;
			if (exceptionHandlerSwitch is null)
			{
				return False(out regions);
			}

			HashSet<ISeseRegion> set = [region];
			Queue<ISeseRegion> queue = new();
			queue.Enqueue(region);
			while (queue.TryDequeue(out ISeseRegion? current))
			{
				foreach (ISeseRegion successor in current.NormalSuccessors)
				{
					ISeseRegion? successorExceptionHandlerSwitch = successor.ExceptionHandlerSwitch;
					if (successorExceptionHandlerSwitch is not null && successorExceptionHandlerSwitch != exceptionHandlerSwitch)
					{
						// try block within a try block
						return False(out regions);
					}
					if (successor.HasAbnormalPredecessors)
					{
						// branch target from leaving an exception handler
					}
					else if (set.Add(successor))
					{
						queue.Enqueue(successor);
					}
				}
			}
			if (set.Count == 1)
			{
				return False(out regions);
			}
			else
			{
				regions = [.. set];
				return true;
			}
		}

		public bool TryMatchExceptionHandlerBlocks([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			if (!region.IsExceptionHandlerEntrypoint || region.IsExceptionHandlerExitpoint)
			{
				regions = null;
				return false;
			}

			// Principle: all exception handlers must contiguous and single-entry single-exit.

			HashSet<ISeseRegion> set = [region];
			Queue<ISeseRegion> queue = new();
			queue.Enqueue(region);
			while (queue.TryDequeue(out ISeseRegion? current))
			{
				foreach (ISeseRegion successor in current.AllSuccessors)
				{
					if (successor.HasAbnormalSuccessors && !successor.IsExceptionHandlerExitpoint)
					{
						// try block within a catch block
						regions = null;
						return false;
					}
					if (set.Add(successor) && !successor.IsExceptionHandlerExitpoint)
					{
						queue.Enqueue(successor);
					}
				}
			}
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

		public bool TryMatchProtectedRegionWithExceptionHandlers([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			// Principle: a protected region of code redirects to an exception handler switch which has at least one exception handler.
			// All the exception handlers must be self-contained and exit to the same place as the protected region would have normally exited.

			ISeseRegion? normalSuccessor;
			if (region is { AllSuccessors.Count: 2, NormalSuccessors.Count: 1 })
			{
				normalSuccessor = region.NormalSuccessors[0];
				if (!normalSuccessor.HasAbnormalPredecessors)
				{
					return False(out regions);
				}
			}
			else if (region is { AllSuccessors.Count: 1, NormalSuccessors.Count: 0 })
			{
				normalSuccessor = null;
			}
			else
			{
				return False(out regions);
			}

			ISeseRegion? exceptionHandlerSwitch = region.ExceptionHandlerSwitch;
			if (exceptionHandlerSwitch is null or { AllPredecessors.Count: not 1 })
			{
				return False(out regions);
			}

			ISeseRegion? parentExceptionHandlerSwitch = null;
			foreach (ISeseRegion exceptionHandler in exceptionHandlerSwitch.AllSuccessors)
			{
				if (!exceptionHandler.IsSelfContainedExceptionHandler)
				{
					return False(out regions);
				}

				foreach (ISeseRegion exceptionHandlerSuccessor in exceptionHandler.AllSuccessors)
				{
					if (exceptionHandlerSuccessor.IsExceptionHandlerSwitch)
					{
					}
					else if (normalSuccessor is null)
					{
						// The protected region has no normal successor, but we still need to check that all the exception handlers lead to the same place.
						normalSuccessor = exceptionHandlerSuccessor;
					}
					else if (exceptionHandlerSuccessor != normalSuccessor)
					{
						return False(out regions);
					}
				}

				ISeseRegion? exceptionHandlerSwitch1 = exceptionHandler.ExceptionHandlerSwitch;
				if (exceptionHandlerSwitch1 is null)
				{
				}
				else if (parentExceptionHandlerSwitch is null)
				{
					parentExceptionHandlerSwitch = exceptionHandlerSwitch1;
				}
				else if (parentExceptionHandlerSwitch != exceptionHandlerSwitch1)
				{
					// Should never happen, except in malformed input.
					return False(out regions);
				}
			}

			regions = [region, exceptionHandlerSwitch, ..exceptionHandlerSwitch.AllSuccessors];
			return true;
		}

		public bool TryMatchSequential([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			if (region.NormalSuccessors.Count != 1)
			{
				// The region must have a single normal successor, but may redirect to an exception handler.
				return False(out regions);
			}
			ISeseRegion normalSuccessor = region.NormalSuccessors[0];
			if (normalSuccessor.AllPredecessors.Count != 1)
			{
				// The successor must only reachable directly from this region.
				return False(out regions);
			}

			ISeseRegion? exceptionHandlerSwitch = region.ExceptionHandlerSwitch;
			if (region.HasNonSwitchAbnormalSuccessor)
			{
				// The region has an abnormal successor, but it's not an exception handler switch.
				return False(out regions);
			}

			if (EqualOrNull(exceptionHandlerSwitch, normalSuccessor.ExceptionHandlerSwitch))
			{
				regions = [region, normalSuccessor];
				return true;
			}
			else
			{
				return False(out regions);
			}
		}

		public bool TryMatchSwitchBlock([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
		{
			// This matches to a traditional switch block, ie where one region has multiple successors that all lead to the same place.

			if (region.NormalSuccessors.Count < 2)
			{
				// The region must have a single normal successor, but may redirect to an exception handler.
				return False(out regions);
			}

			ISeseRegion? exceptionHandlerSwitch = region.ExceptionHandlerSwitch;
			if (region.HasNonSwitchAbnormalSuccessor)
			{
				// The region has an abnormal successor, but it's not an exception handler switch.
				return False(out regions);
			}

			ISeseRegion? ultimateSuccessor = null;
			foreach (ISeseRegion normalSuccessor in region.NormalSuccessors)
			{
				if (normalSuccessor.AllPredecessors.Count != 1)
				{
					// The successor must only reachable directly from this region.
					return False(out regions);
				}
				ISeseRegion? normalSuccessorExceptionHandlerSwitch = normalSuccessor.ExceptionHandlerSwitch;
				if (normalSuccessorExceptionHandlerSwitch is null)
				{
				}
				else if (exceptionHandlerSwitch is null)
				{
					exceptionHandlerSwitch = normalSuccessorExceptionHandlerSwitch;
				}
				else if (normalSuccessorExceptionHandlerSwitch != exceptionHandlerSwitch)
				{
					// The exception handler switch must be consistent.
					return False(out regions);
				}

				if (normalSuccessor.HasNonSwitchAbnormalSuccessor)
				{
					// The successor must not have any abnormal successors, except for the exception handler switch.
					return False(out regions);
				}

				if (normalSuccessor.NormalSuccessors.Count != 1)
				{
					// The successor must have a single normal successor.
					return False(out regions);
				}

				if (ultimateSuccessor is null)
				{
					ultimateSuccessor = normalSuccessor.NormalSuccessors[0];
				}
				else if (normalSuccessor.NormalSuccessors[0] != ultimateSuccessor)
				{
					// The successor must have the same ultimate successor.
					return False(out regions);
				}
			}

			regions = [region, .. region.NormalSuccessors];
			return true;
		}

		public bool HasNonSwitchAbnormalSuccessor => region.HasAbnormalSuccessors && (region.AllSuccessors.Count - region.NormalSuccessors.Count != 1 || region.ExceptionHandlerSwitch is null);
	}

	private static bool False([NotNullWhen(true)] out IReadOnlyList<ISeseRegion>? regions)
	{
		regions = null;
		return false;
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
