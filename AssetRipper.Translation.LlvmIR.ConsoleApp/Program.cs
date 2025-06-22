using AsmResolver.DotNet;

namespace AssetRipper.Translation.LlvmIR.ConsoleApp;

internal static class Program
{
	static void Main()
	{
		Arguments? args = Arguments.Parse();
		if (args is null)
		{
			return;
		}

		ReadOnlySpan<string> mangledNames = args.MangledNames;
		ReadOnlySpan<string> newNames = args.NewNames;
		if (mangledNames.Length != newNames.Length)
		{
			Console.WriteLine("The number of mangled names must be the same as the number of new names");
			return;
		}

		string name = Path.GetFileNameWithoutExtension(args.Input);
		byte[] data = File.ReadAllBytes(args.Input);

		TranslatorOptions options = new()
		{
			FixAssemblyReferences = true,
			Namespace = args.Namespace,
			ModuleName = args.ModuleName,
		};
		for (int i = 0; i < mangledNames.Length; i++)
		{
			options.RenamedSymbols[mangledNames[i]] = newNames[i];
		}

		ModuleDefinition moduleDefinition = Translator.Translate(name, data, options);
		if (string.IsNullOrEmpty(args.DecompileDirectory))
		{
			moduleDefinition.Write($"{moduleDefinition.Name}.dll");
		}
		else
		{
			Directory.CreateDirectory(args.DecompileDirectory);
			new TranslationProjectDecompiler().DecompileProject(moduleDefinition, args.DecompileDirectory, new StringWriter());
		}
		Console.WriteLine("Done!");
	}
}
