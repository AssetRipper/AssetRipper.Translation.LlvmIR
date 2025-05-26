namespace AssetRipper.Translation.Cpp.ExceptionHandling;

[Flags]
public enum SeseRegionType
{
	None = 0,
	ExceptionHandlerEntrypoint = 1,
	ExceptionHandlerExitpoint = 2,
	ExceptionHandlerSwitch = 4,
	CleanupEntrypoint = 8,
	CleanupExitpoint = 16,
}
