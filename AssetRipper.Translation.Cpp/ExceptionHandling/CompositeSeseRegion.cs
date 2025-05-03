namespace AssetRipper.Translation.Cpp.ExceptionHandling;

internal class CompositeSeseRegion(IReadOnlyList<ISeseRegion> children, bool isExceptionHandlerEntrypoint, bool isExceptionHandlerExitpoint, bool isExceptionHandlerSwitch) : ModifiableSeseRegion, ICompositeSeseRegion
{
	public IReadOnlyList<ISeseRegion> Children { get; } = children;
	public override bool IsExceptionHandlerEntrypoint => isExceptionHandlerEntrypoint;
	public override bool IsExceptionHandlerExitpoint => isExceptionHandlerExitpoint;
	public override bool IsExceptionHandlerSwitch => isExceptionHandlerSwitch;
	public override bool IsFunctionEntrypoint => Children.Any(c => c.IsFunctionEntrypoint);

	public CompositeSeseRegion(IReadOnlyList<ISeseRegion> children) : this(children, false, false, false)
	{
	}

	public override string ToString()
	{
		return $"{nameof(CompositeSeseRegion)} {{ {string.Join(", ", Children)} }}";
	}
}
