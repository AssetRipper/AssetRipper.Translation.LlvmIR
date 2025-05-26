namespace AssetRipper.Translation.Cpp.ExceptionHandling;

internal class CompositeSeseRegion(
	IReadOnlyList<ISeseRegion> children,
	bool isExceptionHandlerEntrypoint,
	bool isExceptionHandlerExitpoint,
	bool isExceptionHandlerSwitch,
	bool isCleanupEntrypoint,
	bool isCleanupExitpoint) : ModifiableSeseRegion, ICompositeSeseRegion
{
	public IReadOnlyList<ISeseRegion> Children { get; } = children;
	public override SeseRegionType Type { get; } = GetType(isExceptionHandlerEntrypoint, isExceptionHandlerExitpoint, isExceptionHandlerSwitch, isCleanupEntrypoint, isCleanupExitpoint);
	public override bool IsFunctionEntrypoint => Children.Any(c => c.IsFunctionEntrypoint);

	public CompositeSeseRegion(IReadOnlyList<ISeseRegion> children) : this(children, false, false, false, false, false)
	{
	}

	public override string ToString()
	{
		return $"{GetType().Name} {{ {string.Join(", ", Children)} }}";
	}

	private static SeseRegionType GetType(
		bool isExceptionHandlerEntrypoint,
		bool isExceptionHandlerExitpoint,
		bool isExceptionHandlerSwitch,
		bool isCleanupEntrypoint,
		bool isCleanupExitpoint)
	{
		SeseRegionType type = SeseRegionType.None;
		if (isExceptionHandlerEntrypoint)
		{
			type |= SeseRegionType.ExceptionHandlerEntrypoint;
		}
		if (isExceptionHandlerExitpoint)
		{
			type |= SeseRegionType.ExceptionHandlerExitpoint;
		}
		if (isExceptionHandlerSwitch)
		{
			type |= SeseRegionType.ExceptionHandlerSwitch;
		}
		if (isCleanupEntrypoint)
		{
			type |= SeseRegionType.CleanupEntrypoint;
		}
		if (isCleanupExitpoint)
		{
			type |= SeseRegionType.CleanupExitpoint;
		}
		return type;
	}
}
