namespace AssetRipper.Translation.Cpp.ExceptionHandling;

internal class LiftedSeseRegion(ISeseRegion original) : ModifiableSeseRegion, ISeseRegionAlias
{
	public ISeseRegion Original { get; } = original;
	public override bool IsExceptionHandlerEntrypoint => Original.IsExceptionHandlerEntrypoint;
	public override bool IsExceptionHandlerExitpoint => Original.IsExceptionHandlerExitpoint;
	public override bool IsExceptionHandlerSwitch => Original.IsExceptionHandlerSwitch;
	public override bool IsFunctionEntrypoint => Original.IsFunctionEntrypoint;
	public override string ToString()
	{
		return Original.ToString() ?? nameof(LiftedSeseRegion);
	}
}
