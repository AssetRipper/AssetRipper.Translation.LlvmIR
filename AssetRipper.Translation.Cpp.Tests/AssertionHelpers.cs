using AsmResolver.DotNet;
using NUnit.Framework;

namespace AssetRipper.Translation.Cpp.Tests;

internal static class AssertionHelpers
{
	public static void AssertSuccessfullySaves(ModuleDefinition module)
	{
		Assert.DoesNotThrow(() =>
		{
			using MemoryStream stream = new();
			module.Write(stream);
		});
	}

	public static void AssertPublicMethodCount(TypeDefinition type, int count)
	{
		Assert.That(type.Methods.Count(m => m.IsPublic), Is.EqualTo(count));
	}

	public static void AssertPublicFieldCount(TypeDefinition type, int count)
	{
		Assert.That(type.Fields.Count(f => f.IsPublic), Is.EqualTo(count));
	}
}
