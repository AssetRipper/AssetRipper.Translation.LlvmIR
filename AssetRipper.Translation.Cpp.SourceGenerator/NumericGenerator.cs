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
		("Int128", "UInt128"),
	];

	private static IEnumerable<string> IntegerTypes => IntegerTypePairs.SelectMany(pair => (IEnumerable<string>)[pair.Signed, pair.Unsigned]);

	private static IEnumerable<(string Operation, char Symbol, string RequiredInterface)> SimpleOperations =>
	[
		("Add", '+', "IAdditionOperators<T, T, T>"),
		("Subtract", '-', "ISubtractionOperators<T, T, T>"),
		("Multiply", '*', "IMultiplyOperators<T, T, T>"),
		("Divide", '/', "IDivisionOperators<T, T, T>"),
		("Remainder", '%', "IModulusOperators<T, T, T>"),
	];

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
		writer.WriteUsing("System.Numerics");
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
			foreach ((string operation, char symbol, string requiredInterface) in SimpleOperations)
			{
				writer.WriteLineNoTabs();
				writer.WriteLine($"[MethodImpl(MethodImplOptions.AggressiveInlining)]");
				writer.WriteLine($"public static T {operation}<T>(T x, T y) where T : {requiredInterface}");
				using (new CurlyBrackets(writer))
				{
					writer.WriteLine($"return unchecked(x {symbol} y);");
				}
				foreach (bool emittingSignedMethod in (ReadOnlySpan<bool>)[true, false])
				{
					writer.WriteLineNoTabs();
					writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
					string methodName = emittingSignedMethod ? $"{operation}Signed" : $"{operation}Unsigned";
					writer.WriteLine($"public static T {methodName}<T>(T x, T y) where T : {requiredInterface}");
					using (new CurlyBrackets(writer))
					{
						using (new Checked(writer))
						{
							foreach ((string signedType, string unsignedType) in IntegerTypePairs)
							{
								(string operationType, string parameterType) = emittingSignedMethod
									? (signedType, unsignedType)
									: (unsignedType, signedType);
								writer.WriteLine($"if (typeof(T) == typeof({parameterType}))");
								using (new CurlyBrackets(writer))
								{
									writer.WriteLine($"{parameterType} x1 = ({parameterType})(object)x;");
									writer.WriteLine($"{parameterType} y1 = ({parameterType})(object)y;");
									writer.WriteLine($"{operationType} result1 = ({operationType})(unchecked(({operationType})x1) {symbol} unchecked(({operationType})y1));");
									writer.WriteLine($"{parameterType} result2 = unchecked(({parameterType})result1);");
									writer.WriteLine("return (T)(object)result2;");
								}
							}
							using (new Else(writer))
							{
								writer.WriteLine($"return x {symbol} y;");
							}
						}
					}
				}
			}
		}

		return stringWriter.ToString();
	}
}
