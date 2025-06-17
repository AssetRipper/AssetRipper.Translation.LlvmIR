namespace AssetRipper.Translation.Cpp;

internal class ExceptionInfo : IDisposable
{
	[ThreadStatic]
	public static ExceptionInfo? Current;

	public virtual string? GetMessage()
	{
		return null;
	}

	protected virtual void Dispose(bool disposing)
	{
	}

	~ExceptionInfo()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: false);
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
