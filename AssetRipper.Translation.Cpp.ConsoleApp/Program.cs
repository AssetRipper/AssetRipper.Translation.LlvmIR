using AsmResolver.DotNet;

namespace AssetRipper.Translation.Cpp.ConsoleApp;

internal static class Program
{
	static void Main(string[] args)
	{
		string inputPath = args[0];
		string name = Path.GetFileNameWithoutExtension(inputPath);
		byte[] data = File.ReadAllBytes(inputPath);

		ModuleDefinition moduleDefinition = CppTranslator.Translate(name, data, true);
		moduleDefinition.Write("ConvertedCpp.dll");
		Console.WriteLine("Done!");
	}
}
