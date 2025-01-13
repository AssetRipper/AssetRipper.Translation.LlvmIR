using AsmResolver.DotNet;
using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.Cpp.Tests;

internal static class Extensions
{
	public static TypeDefinition GetGlobalMembersType(this ModuleDefinition module)
	{
		return module.TopLevelTypes.Single(t => t.Namespace is null && t.Name == "GlobalMembers");
	}

	public static TypeDefinition GetConstantsType(this ModuleDefinition module)
	{
		return module.TopLevelTypes.Single(t => t.Namespace is null && t.Name == "Constants");
	}

	public static ModuleDefinition TranslateToCIL(this string text, [CallerMemberName] string? caller = null)
	{
		string name = string.IsNullOrEmpty(caller) ? nameof(TranslateToCIL) : caller;
		return CppTranslator.Translate(name, text);
	}
}
