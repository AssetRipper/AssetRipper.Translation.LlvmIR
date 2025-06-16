namespace AssetRipper.Translation.Cpp;

internal class ExceptionInfo
{
	[ThreadStatic]
	public static ExceptionInfo? Current;

	public virtual string? GetMessage()
	{
		return null;
	}
}
