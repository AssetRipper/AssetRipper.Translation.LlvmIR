using AssetRipper.Text.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SGF;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Tests.SourceGenerator;

[IncrementalGenerator]
public partial class TestGenerator() : IncrementalGenerator(nameof(TestGenerator))
{
	private const string Namespace = "AssetRipper.Translation.Cpp.Tests";
	private const string SavesSuccessfullyAttribute = "SavesSuccessfullyAttribute";
	private const string DecompilesSuccessfullyAttribute = "DecompilesSuccessfullyAttribute";
	private const string RecompilesSuccessfullyAttribute = "RecompilesSuccessfullyAttribute";
	private const string SavesSuccessfullyAttributeFullName = Namespace + "." + SavesSuccessfullyAttribute;
	private const string DecompilesSuccessfullyAttributeFullName = Namespace + "." + DecompilesSuccessfullyAttribute;
	private const string RecompilesSuccessfullyAttributeFullName = Namespace + "." + RecompilesSuccessfullyAttribute;

	private readonly record struct FieldInfo(string? TypeNamespace, string TypeName, string FieldName);

	public override void OnInitialize(SgfInitializationContext context)
	{
		context.RegisterPostInitializationOutput(AddAttributes);

		context.RegisterSourceOutput(GetFields(context, SavesSuccessfullyAttributeFullName).Collect(), AddSavesSuccessfullyTests);
		context.RegisterSourceOutput(GetFields(context, DecompilesSuccessfullyAttributeFullName).Collect(), AddDecompilesSuccessfullyTests);
		context.RegisterSourceOutput(GetFields(context, RecompilesSuccessfullyAttributeFullName).Collect(), AddRecompilesSuccessfullyTests);
	}

	private static void AddSavesSuccessfullyTests(SgfSourceProductionContext context, ImmutableArray<FieldInfo> array)
	{
		AddTests(context, array, "SavesSuccessfullyTests.g.cs", "SavesSuccessfully");
	}

	private static void AddDecompilesSuccessfullyTests(SgfSourceProductionContext context, ImmutableArray<FieldInfo> array)
	{
		AddTests(context, array, "DecompilesSuccessfullyTests.g.cs", "DecompilesSuccessfully");
	}

	private static void AddRecompilesSuccessfullyTests(SgfSourceProductionContext context, ImmutableArray<FieldInfo> array)
	{
	}

	private static void AddTests(SgfSourceProductionContext context, ImmutableArray<FieldInfo> array, string fileName, string methodName)
	{
		using StringWriter stringWriter = new() { NewLine = "\n" };
		using IndentedTextWriter writer = IndentedTextWriterFactory.Create(stringWriter);

		writer.WriteGeneratedCodeWarning();
		foreach (FieldInfo fieldInfo in array)
		{
			writer.WriteLineNoTabs();
			writer.WriteComment(fieldInfo.FieldName);
			if (fieldInfo.TypeNamespace is not null)
			{
				using (new Namespace(writer, fieldInfo.TypeNamespace))
				{
					WriteTest(writer, fieldInfo, methodName);
				}
			}
			else
			{
				WriteTest(writer, fieldInfo, methodName);
			}
		}

		context.AddSource(fileName, stringWriter.ToString());

		static void WriteTest(IndentedTextWriter writer, FieldInfo fieldInfo, string methodName)
		{
			writer.WriteLine($"partial class {fieldInfo.TypeName}");
			using (new CurlyBrackets(writer))
			{
				writer.WriteLine("[global::NUnit.Framework.Test]");
				writer.WriteLine($"public void {fieldInfo.FieldName}_{methodName}()");
				using (new CurlyBrackets(writer))
				{
					writer.WriteLine($"AssertionHelpers.Assert{methodName}({fieldInfo.FieldName}.TranslateToCIL());");
				}
			}
		}
	}

	private static IncrementalValuesProvider<FieldInfo> GetFields(SgfInitializationContext context, string attributeFullName)
	{
		return context.SyntaxProvider.ForAttributeWithMetadataName(attributeFullName, (syntaxNode, ct) =>
		{
			return syntaxNode is VariableDeclaratorSyntax variable
				&& variable.Parent?.Parent is BaseFieldDeclarationSyntax field
				&& field.Parent is ClassDeclarationSyntax type
				&& type.Modifiers.Any(SyntaxKind.PartialKeyword);
		},
		(context, ct) =>
		{
			VariableDeclaratorSyntax variable = (VariableDeclaratorSyntax)context.TargetNode;
			BaseFieldDeclarationSyntax field = (BaseFieldDeclarationSyntax)(variable.Parent?.Parent ?? throw new());
			ClassDeclarationSyntax parent = (ClassDeclarationSyntax)(field.Parent ?? throw new());
			string? @namespace = parent.Parent switch
			{
				BaseNamespaceDeclarationSyntax ns => ns.Name.ToString(),
				CompilationUnitSyntax => null,
				_ => throw new NotSupportedException(),
			};
			string typeName = parent.Identifier.ToString();
			string fieldName = field.Declaration.Variables[0].Identifier.ToString();
			return new FieldInfo(@namespace, typeName, fieldName);
		});
	}

	private static void AddAttributes(IncrementalGeneratorPostInitializationContext context)
	{
		context.AddSource(SavesSuccessfullyAttribute + ".g.cs", GetFieldAttributeText(Namespace, SavesSuccessfullyAttribute));
		context.AddSource(DecompilesSuccessfullyAttribute + ".g.cs", GetFieldAttributeText(Namespace, DecompilesSuccessfullyAttribute));
		context.AddSource(RecompilesSuccessfullyAttribute + ".g.cs", GetFieldAttributeText(Namespace, RecompilesSuccessfullyAttribute));
	}

	private static string GetFieldAttributeText(string @namespace, string name)
	{
		return $$"""
			namespace {{@namespace}}
			{
				[global::System.AttributeUsage(global::System.AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
				public sealed class {{name}} : global::System.Attribute
				{
				}
			}
			""";
	}
}
