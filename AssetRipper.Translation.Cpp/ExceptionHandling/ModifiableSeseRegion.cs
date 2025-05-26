using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.ExceptionHandling;

internal abstract class ModifiableSeseRegion : ISeseRegion
{
	public CombinedList<ISeseRegion> AllPredecessors { get; }
	public CombinedList<ISeseRegion> AllSuccessors { get; }
	public List<ISeseRegion> NormalPredecessors { get; } = [];
	public List<ISeseRegion> NormalSuccessors { get; } = [];
	public List<ISeseRegion> InvokePredecessors { get; } = [];
	public List<ISeseRegion> InvokeSuccessors { get; } = [];
	public List<ISeseRegion> HandlerPredecessors { get; } = [];
	public List<ISeseRegion> HandlerSuccessors { get; } = [];

	public ModifiableSeseRegion()
	{
		AllPredecessors = new CombinedList<ISeseRegion>(NormalPredecessors, InvokePredecessors, HandlerPredecessors);
		AllSuccessors = new CombinedList<ISeseRegion>(NormalSuccessors, InvokeSuccessors, HandlerSuccessors);
	}

	public abstract SeseRegionType Type { get; }
	public abstract bool IsFunctionEntrypoint { get; }

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
}
