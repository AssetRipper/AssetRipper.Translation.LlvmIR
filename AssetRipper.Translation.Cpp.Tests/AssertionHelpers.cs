using AsmResolver.DotNet;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using NUnit.Framework;
using System.Diagnostics;

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
			projectDecompiler.DecompileProject(moduleFile, directory);

			Assert.That(Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories), Has.Length.GreaterThan(0));

			ProcessStartInfo startInfo = new ProcessStartInfo("dotnet", $"build \"{directory}\"")
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			/*using Process? process = Process.Start(startInfo);
			if (process is null)
			{
				Assert.Fail("Failed to start dotnet build process");
				return;
			}

			process.WaitForExit();
			string output = process.StandardOutput.ReadToEnd();
			string error = process.StandardError.ReadToEnd();
			Assert.Multiple(() =>
			{
				Assert.That(output, Is.Empty, $"Output: {output}");
				Assert.That(error, Is.Empty, $"Error: {error}");
				Assert.That(process.ExitCode, Is.Zero);
			});*/
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
}
