﻿namespace AssetRipper.Translation.LlvmIR;

internal abstract class NameAttribute(string name) : Attribute
{
	public string Name { get; } = name;
}
