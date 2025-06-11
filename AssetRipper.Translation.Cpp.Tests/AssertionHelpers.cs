using AsmResolver.DotNet;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using NUnit.Framework;
using System.Numerics.Tensors;
using System.Text;

namespace AssetRipper.Translation.Cpp.Tests;

internal static class AssertionHelpers
{
	public static void AssertSavesSuccessfully(ModuleDefinition module)
	{
		Assert.DoesNotThrow(() =>
		{
			using MemoryStream stream = new();
			module.Write(stream);
		});
	}

	public static void AssertDecompilesSuccessfully(ModuleDefinition module)
	{
		string file = Path.GetTempFileName();
		string directory = Path.GetRandomFileName();
		Directory.CreateDirectory(directory);
		try
		{
			using(FileStream fileStream = new(file, FileMode.Open, FileAccess.Write))
			{
				module.Write(fileStream);
			}

			using PEFile moduleFile = new(file);
			UniversalAssemblyResolver assemblyResolver = new(null, true, ".NETCoreApp,Version=v9.0");
			assemblyResolver.AddSearchDirectory(AppContext.BaseDirectory); // for any NuGet references
			WholeProjectDecompiler projectDecompiler = new(assemblyResolver);
			projectDecompiler.Settings.CheckForOverflowUnderflow = true;
			projectDecompiler.DecompileProject(moduleFile, directory);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories), Has.Length.GreaterThan(0));
				Assert.That(SuccessfullyCompiles(directory));
			}
		}
		finally
		{
			File.Delete(file);
			Directory.Delete(directory, true);
		}
	}

	public static void AssertPublicMethodCount(TypeDefinition type, int count)
	{
		Assert.That(type.Methods.Count(m => m.IsPublic), Is.EqualTo(count));
	}

	public static void AssertPublicFieldCount(TypeDefinition type, int count)
	{
		Assert.That(type.Fields.Count(f => f.IsPublic), Is.EqualTo(count));
	}

	private static bool SuccessfullyCompiles(string directory)
	{
		List<SyntaxTree> syntaxTrees = [];

		foreach (string path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
		{
			AddCode(syntaxTrees, File.ReadAllText(path));
		}

		IEnumerable<MetadataReference> references =
		[
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
			MetadataReference.CreateFromFile(GetAssemblyPath("System.Runtime.dll")),
			MetadataReference.CreateFromFile(typeof(TensorPrimitives).Assembly.Location),
		];

		using MemoryStream polyfillOutputStream = new();

		// Emit compiled assembly into MemoryStream
		CSharpCompilation compilation = CreateCompilation(syntaxTrees, references);
		EmitResult result = compilation.Emit(polyfillOutputStream);
		return result.Success;

		static void AddCode(List<SyntaxTree> syntaxTrees, string code)
		{
			SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, encoding: Encoding.UTF8);
			syntaxTrees.Add(syntaxTree);
		}

		static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> syntaxTrees, IEnumerable<MetadataReference> references)
		{
			// Define compilation options
			CSharpCompilationOptions compilationOptions = new(OutputKind.DynamicallyLinkedLibrary, checkOverflow: true, allowUnsafe: true);

			// Create the compilation
			CSharpCompilation compilation = CSharpCompilation.Create(
				"ConvertedCode",
				syntaxTrees,
				references,
				compilationOptions
			);
			return compilation;
		}

		static string GetAssemblyPath(string fileName)
		{
			string coreLibPath = typeof(object).Assembly.Location;
			string directory = Path.GetDirectoryName(coreLibPath) ?? throw new InvalidOperationException("Could not determine directory of core library.");
			return Path.Combine(directory, fileName);
		}
	}
}
