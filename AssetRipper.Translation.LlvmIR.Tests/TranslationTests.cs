using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using NUnit.Framework;

namespace AssetRipper.Translation.LlvmIR.Tests;

[NonParallelizable]
public class TranslationTests
{
	[Test]
	public void NoopTest()
	{
		const string Text = """
			define dso_local i32 @do_nothing(i32 noundef %0) #0 {
			  %2 = alloca i32, align 4
			  store i32 %0, ptr %2, align 4
			  %3 = load i32, ptr %2, align 4
			  ret i32 %3
			}
			""";
		ModuleDefinition module = Text.TranslateToCIL();

		AssertionHelpers.AssertPublicMethodCount(module.GetGlobalFunctionsType(), 1);
		AssertionHelpers.AssertPublicFieldCount(module.GetPointerCacheType(), 0);

		MethodDefinition method = module.GetGlobalFunctionsType().Methods.First(m => m.IsPublic);
		Assert.Multiple(() =>
		{
			Assert.That(method.Name, Is.EqualTo("do_nothing"));
			Assert.That(method.Parameters, Has.Count.EqualTo(1));
			Assert.That(method.Signature?.ReturnType is CorLibTypeSignature { ElementType: ElementType.I4 });
			Assert.That(method.Signature?.ParameterTypes[0] is CorLibTypeSignature { ElementType: ElementType.I4 });
		});

		AssertionHelpers.AssertSavesSuccessfully(module);
		AssertionHelpers.AssertDecompilesSuccessfully(module);
	}

	[Test]
	public void GlobalVariableTest()
	{
		const string Text = """
			@X = global i32 17
			@Y = global i32 42
			@Z = global [2 x ptr] [ ptr @X, ptr @Y ]
			""";
		ModuleDefinition module = Text.TranslateToCIL();

		AssertionHelpers.AssertPublicMethodCount(module.GetGlobalFunctionsType(), 0);
		AssertionHelpers.AssertPublicFieldCount(module.GetPointerCacheType(), 3);

		AssertionHelpers.AssertSavesSuccessfully(module);
	}
}
