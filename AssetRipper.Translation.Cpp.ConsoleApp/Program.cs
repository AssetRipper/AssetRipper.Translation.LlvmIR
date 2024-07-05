namespace AssetRipper.Translation.Cpp.ConsoleApp;

internal static class Program
{
	static void Main(string[] args)
	{
		CppTranslator.Translate(args[0], "ConvertedCpp.dll");
		Console.WriteLine("Done!");
	}
}
