﻿namespace AssetRipper.Translation.LlvmIR;

public sealed class FatalException : Exception
{
	public FatalException()
	{
	}

	public FatalException(string? message) : base(message)
	{
	}
}
