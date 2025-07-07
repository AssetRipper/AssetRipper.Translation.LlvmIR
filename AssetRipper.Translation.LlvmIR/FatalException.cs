namespace AssetRipper.Translation.LlvmIR;

internal sealed class FatalException : Exception
{
	public FatalException()
	{
	}

	public FatalException(string? message) : base(message)
	{
	}
}
