using AsmResolver.DotNet;

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
}
