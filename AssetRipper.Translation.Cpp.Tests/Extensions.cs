using AsmResolver.DotNet;
using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.Cpp.Tests;

internal static class Extensions
{
	public static TypeDefinition GetGlobalFunctionsType(this ModuleDefinition module)
	{
		return module.TopLevelTypes.Single(t => t.Namespace is null && t.Name == "GlobalFunctions");
	}

	public static TypeDefinition GetPointerCacheType(this ModuleDefinition module)
	{
		return module.TopLevelTypes.Single(t => t.Namespace is null && t.Name == "PointerCache");
	}

	public static ModuleDefinition TranslateToCIL(this string text, [CallerMemberName] string? caller = null)
	{
		string name = string.IsNullOrEmpty(caller) ? nameof(TranslateToCIL) : caller;
		return Translator.Translate(name, text);
	}
}
