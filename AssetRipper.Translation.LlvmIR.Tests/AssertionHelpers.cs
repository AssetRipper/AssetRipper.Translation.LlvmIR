using AsmResolver.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Collections.Concurrent;
using System.Numerics.Tensors;
using System.Text;

namespace AssetRipper.Translation.LlvmIR.Tests;

internal static class AssertionHelpers
{
	public static Task AssertSavesSuccessfully(ModuleDefinition module)
	{
		using MemoryStream stream = new();
		module.Write(stream);
		return Task.CompletedTask;
	}

	public static async Task AssertDecompilesSuccessfully(ModuleDefinition module)
	{
		string directory = Path.GetRandomFileName();
		Directory.CreateDirectory(directory);
		try
		{
			new TranslationProjectDecompiler().DecompileProject(module, directory);

			using (Assert.Multiple())
			{
				await Assert.That(Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)).Count().IsGreaterThan(0);
				await Assert.That(SuccessfullyCompiles(directory)).IsTrue();
			}
		}
		finally
		{
			Directory.Delete(directory, true);
		}
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
			MetadataReference.CreateFromFile(typeof(ConcurrentDictionary<,>).Assembly.Location),
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
