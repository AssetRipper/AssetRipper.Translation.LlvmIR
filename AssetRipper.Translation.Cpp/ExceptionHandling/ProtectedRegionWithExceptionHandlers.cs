namespace AssetRipper.Translation.Cpp.ExceptionHandling;

internal class ProtectedRegionWithExceptionHandlers : CompositeSeseRegion
{
	public ISeseRegion ProtectedRegion { get; }
	public IEnumerable<ISeseRegion> ExceptionHandlingRegions => Children.Where(c => c != ProtectedRegion);

	public ProtectedRegionWithExceptionHandlers(ISeseRegion protectRegion, IReadOnlyList<ISeseRegion> allRegions)
		: base(allRegions, protectRegion.IsExceptionHandlerEntrypoint, protectRegion.IsExceptionHandlerExitpoint, protectRegion.IsExceptionHandlerSwitch, protectRegion.IsCleanupEntrypoint, protectRegion.IsCleanupExitpoint)
	{
		ProtectedRegion = protectRegion;
	}
}
