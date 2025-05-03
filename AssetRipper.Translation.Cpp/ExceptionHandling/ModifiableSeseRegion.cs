using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.ExceptionHandling;

internal abstract class ModifiableSeseRegion : ISeseRegion
{
	public List<ISeseRegion> AllPredecessors { get; } = [];
	public List<ISeseRegion> AllSuccessors { get; } = [];
	public List<ISeseRegion> NormalPredecessors { get; } = [];
	public List<ISeseRegion> NormalSuccessors { get; } = [];
	public abstract bool IsExceptionHandlerEntrypoint { get; }
	public abstract bool IsExceptionHandlerExitpoint { get; }
	public abstract bool IsExceptionHandlerSwitch { get; }
	public abstract bool IsFunctionEntrypoint { get; }

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.AllPredecessors => AllPredecessors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.AllSuccessors => AllSuccessors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.NormalPredecessors => NormalPredecessors;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	IReadOnlyList<ISeseRegion> ISeseRegion.NormalSuccessors => NormalSuccessors;
}
