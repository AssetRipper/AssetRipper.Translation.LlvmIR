using AsmResolver.DotNet;
using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.LlvmIR.Tests;

internal static class Extensions
{
	public static ModuleDefinition TranslateToCIL(this string text, [CallerMemberName] string? caller = null)
	{
		string name = string.IsNullOrEmpty(caller) ? nameof(TranslateToCIL) : caller;
		return Translator.Translate(name, text);
	}
}
