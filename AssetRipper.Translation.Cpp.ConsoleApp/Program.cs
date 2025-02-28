using AsmResolver.DotNet;

namespace AssetRipper.Translation.Cpp.ConsoleApp;

internal static class Program
{
	static void Main()
	{
		Arguments? args = Arguments.Parse();
		if (args is null)
		{
			return;
		}

		string name = Path.GetFileNameWithoutExtension(args.Input);
		byte[] data = File.ReadAllBytes(args.Input);

		ModuleDefinition moduleDefinition = CppTranslator.Translate(name, data, true);
		moduleDefinition.Write("ConvertedCpp.dll");
		Console.WriteLine("Done!");
	}
}
