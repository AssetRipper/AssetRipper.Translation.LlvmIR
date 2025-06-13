using AssetRipper.Text.SourceGeneration;
using SGF;
using System.CodeDom.Compiler;

namespace AssetRipper.Translation.Cpp.SourceGenerator;

[IncrementalGenerator]
public partial class NumericGenerator() : IncrementalGenerator(nameof(NumericGenerator))
{
	private static IEnumerable<(string Signed, string Unsigned)> IntegerTypePairs =>
	[
		("sbyte", "byte"),
		("short", "ushort"),
		("int", "uint"),
		("nint", "nuint"),
		("long", "ulong"),
	];

	private static IEnumerable<string> IntegerTypes => IntegerTypePairs.SelectMany(pair => (IEnumerable<string>)[pair.Signed, pair.Unsigned]);

	public override void OnInitialize(SgfInitializationContext context)
	{
		context.RegisterPostInitializationOutput(static context =>
		{
			context.AddSource("NumericHelper.g.cs", GenerateNumericHelper());
		});
	}

	private static string GenerateNumericHelper()
	{
		StringWriter stringWriter = new() { NewLine = "\n" };
		IndentedTextWriter writer = IndentedTextWriterFactory.Create(stringWriter);

		writer.WriteGeneratedCodeWarning();
		writer.WriteLineNoTabs();
		writer.WriteUsing("System.Runtime.CompilerServices");
		writer.WriteLineNoTabs();
		writer.WriteFileScopedNamespace("AssetRipper.Translation.Cpp");
		writer.WriteLineNoTabs();
		writer.WriteLine("static partial class NumericHelper");
		using (new CurlyBrackets(writer))
		{
			writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
			writer.WriteLine("private static int ConvertToInt32<T>(T x)");
			using (new CurlyBrackets(writer))
			{
				foreach (string type in IntegerTypes)
				{
					writer.WriteLine($"if (typeof(T) == typeof({type}))");
					using (new CurlyBrackets(writer))
					{
						writer.WriteLine($"return unchecked((int)({type})(object)x);");
					}
				}
				using (new Else(writer))
				{
					writer.WriteLine("return default;");
				}
			}
		}

		return stringWriter.ToString();
	}
}
