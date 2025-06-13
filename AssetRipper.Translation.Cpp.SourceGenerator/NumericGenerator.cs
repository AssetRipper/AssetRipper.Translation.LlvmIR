using AssetRipper.Text.SourceGeneration;
using SGF;
using System.CodeDom.Compiler;

namespace AssetRipper.Translation.Cpp.SourceGenerator;

[IncrementalGenerator]
public partial class NumericGenerator() : IncrementalGenerator(nameof(NumericGenerator))
{
	[Flags]
	private enum OperandSupport
	{
		None = 0,
		Integer = 1 << 0,
		FloatingPoint = 1 << 1,
		All = Integer | FloatingPoint,
	}

	private static IEnumerable<(string Signed, string Unsigned)> IntegerTypePairs =>
	[
		("sbyte", "byte"),
		("short", "ushort"),
		("int", "uint"),
		("long", "ulong"),
		("Int128", "UInt128"),
	];

	private static IEnumerable<string> IntegerTypes => IntegerTypePairs.SelectMany(pair => (IEnumerable<string>)[pair.Signed, pair.Unsigned]);

	private static IEnumerable<string> FloatingPointTypes => [ "Half", "float", "double" ];

	private static IEnumerable<string> NumericTypes => IntegerTypes.Concat(FloatingPointTypes);

	private static IEnumerable<string> GetTypes(OperandSupport support) => support switch
	{
		OperandSupport.Integer => IntegerTypes,
		OperandSupport.FloatingPoint => FloatingPointTypes,
		OperandSupport.All => NumericTypes,
		_ => [],
	};

	private static IEnumerable<(string Operation, char Symbol, string RequiredInterfaces)> SimpleOperations =>
	[
		("Add", '+', "IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>"),
		("Subtract", '-', "ISubtractionOperators<T, T, T>"),
		("Multiply", '*', "IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>"),
		("Divide", '/', "IDivisionOperators<T, T, T>"),
		("Remainder", '%', "IModulusOperators<T, T, T>"),
	];

	private static IEnumerable<(string Name, string? ImplementationType, string? ImplementationName, OperandSupport)> UnaryOperations =>
	[
		("BSwap", "System.Buffers.Binary.BinaryPrimitives", "ReverseEndianness", OperandSupport.Integer),
	];

	private static IEnumerable<(string LlvmName, string? DotNetName, string RequiredInterfaces)> UnaryInterfaceOperations =>
	[
		("Abs", null, "INumberBase<T>"),
		("FAbs", "Abs", "INumberBase<T>"),
		("Sin", null, "ITrigonometricFunctions<T>"),
		("Cos", null, "ITrigonometricFunctions<T>"),
		("Tan", null, "ITrigonometricFunctions<T>"),
		("Asin", null, "ITrigonometricFunctions<T>"),
		("Acos", null, "ITrigonometricFunctions<T>"),
		("Atan", null, "ITrigonometricFunctions<T>"),
		("Sinh", null, "IHyperbolicFunctions<T>"),
		("Cosh", null, "IHyperbolicFunctions<T>"),
		("Tanh", null, "IHyperbolicFunctions<T>"),
		("Exp", null, "IExponentialFunctions<T>"),
		("Exp2", null, "IExponentialFunctions<T>"),
		("Exp10", null, "IExponentialFunctions<T>"),
		("Log", null, "ILogarithmicFunctions<T>"),
		("Log2", null, "ILogarithmicFunctions<T>"),
		("Log10", null, "ILogarithmicFunctions<T>"),
		("Sqrt", null, "IRootFunctions<T>"),
		("Ceil", "Ceiling", "IFloatingPoint<T>"),
		("Floor", null, "IFloatingPoint<T>"),
		("Round", null, "IFloatingPoint<T>"),
		("CtLz", "LeadingZeroCount", "IBinaryInteger<T>"),
	];

	private static IEnumerable<(string LlvmName, string? DotNetName, string RequiredInterfaces)> BinaryInterfaceOperations =>
	[
		("Pow", null, "IPowerFunctions<T>"),
	];

	public override void OnInitialize(SgfInitializationContext context)
	{
		context.RegisterPostInitializationOutput(static context =>
		{
			context.AddSource("NumericHelper.g.cs", GenerateNumericHelper());
			context.AddSource("InlineArrayNumericHelper.g.cs", GenerateInlineArrayNumericHelper());
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
			writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
			writer.WriteLine("private static T ConvertFromInt32<T>(int x)");
			using (new CurlyBrackets(writer))
			{
				foreach (string type in IntegerTypes)
				{
					writer.WriteLine($"if (typeof(T) == typeof({type}))");
					using (new CurlyBrackets(writer))
					{
						writer.WriteLine($"return unchecked((T)(object)({type})x);");
					}
				}
				using (new Else(writer))
				{
					writer.WriteLine("return default;");
				}
			}
			foreach ((string operation, char symbol, string requiredInterfaces) in SimpleOperations)
			{
				writer.WriteLineNoTabs();
				writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
				writer.WriteLine($"public static T {operation}<T>(T x, T y) where T : {requiredInterfaces}");
				using (new CurlyBrackets(writer))
				{
					writer.WriteLine($"return unchecked(x {symbol} y);");
				}
				foreach (bool emittingSignedMethod in (ReadOnlySpan<bool>)[true, false])
				{
					writer.WriteLineNoTabs();
					writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
					string methodName = emittingSignedMethod ? $"{operation}Signed" : $"{operation}Unsigned";
					writer.WriteLine($"public static T {methodName}<T>(T x, T y) where T : {requiredInterfaces}");
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
			foreach ((string name, string? implementationType, string? implementationName, OperandSupport support) in UnaryOperations)
			{
				writer.WriteLineNoTabs();
				writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
				writer.WriteLine($"public static T {name}<T>(T x, T y)");
				using (new CurlyBrackets(writer))
				{
					foreach (string type in GetTypes(support))
					{
						writer.WriteLine($"if (typeof(T) == typeof({type}))");
						using (new CurlyBrackets(writer))
						{
							string actualImplementationType = string.IsNullOrEmpty(implementationType) ? type : $"global::{implementationType}";
							string actualImplementationName = string.IsNullOrEmpty(implementationName) ? name : implementationName!;
							writer.WriteLine($"return (T)(object)({type}){actualImplementationType}.{actualImplementationName}(({type})(object)x);");
						}
					}
					using (new Else(writer))
					{
						writer.WriteLine("return default;");
					}
				}
			}
			foreach ((string llvmName, string? dotNetName, string requiredInterfaces) in UnaryInterfaceOperations)
			{
				writer.WriteLineNoTabs();
				writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
				string methodName = string.IsNullOrEmpty(dotNetName) ? llvmName : dotNetName!;
				writer.WriteLine($"public static T {llvmName}<T>(T x) where T : {requiredInterfaces}");
				using (new CurlyBrackets(writer))
				{
					writer.WriteLine($"return T.{methodName}(x);");
				}
			}
			foreach ((string llvmName, string? dotNetName, string requiredInterfaces) in BinaryInterfaceOperations)
			{
				writer.WriteLineNoTabs();
				writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
				string methodName = string.IsNullOrEmpty(dotNetName) ? llvmName : dotNetName!;
				writer.WriteLine($"public static T {llvmName}<T>(T x, T y) where T : {requiredInterfaces}");
				using (new CurlyBrackets(writer))
				{
					writer.WriteLine($"return T.{methodName}(x, y);");
				}
			}
		}

		return stringWriter.ToString();
	}

	private static string GenerateInlineArrayNumericHelper()
	{
		StringWriter stringWriter = new() { NewLine = "\n" };
		IndentedTextWriter writer = IndentedTextWriterFactory.Create(stringWriter);

		writer.WriteGeneratedCodeWarning();
		writer.WriteLineNoTabs();
		writer.WriteUsing("System.Numerics");
		writer.WriteUsing("System.Numerics.Tensors");
		writer.WriteUsing("System.Runtime.CompilerServices");
		writer.WriteLineNoTabs();
		writer.WriteFileScopedNamespace("AssetRipper.Translation.Cpp");
		writer.WriteLineNoTabs();
		writer.WriteLine("static partial class InlineArrayNumericHelper");
		using (new CurlyBrackets(writer))
		{
			foreach ((string operation, char symbol, string requiredInterfaces) in SimpleOperations)
			{
				WriteBinaryTensorPrimitivesMethod(writer, operation, null, ReplaceTypeParameterName(requiredInterfaces));
				WriteBinaryNumericHelperMethod(writer, $"{operation}Signed", ReplaceTypeParameterName(requiredInterfaces));
				WriteBinaryNumericHelperMethod(writer, $"{operation}Unsigned", ReplaceTypeParameterName(requiredInterfaces));
			}
			WriteBinaryNumericHelperMethod(writer, "ShiftLeft", "IShiftOperators<TElement, int, TElement>");
			WriteBinaryNumericHelperMethod(writer, "ShiftRightLogical", "IShiftOperators<TElement, int, TElement>");
			WriteBinaryNumericHelperMethod(writer, "ShiftRightArithmetic", "IShiftOperators<TElement, int, TElement>");
			WriteBinaryTensorPrimitivesMethod(writer, "BitwiseAnd", null, "IBitwiseOperators<TElement, TElement, TElement>");
			WriteBinaryTensorPrimitivesMethod(writer, "BitwiseOr", null, "IBitwiseOperators<TElement, TElement, TElement>");
			WriteBinaryNumericHelperMethod(writer, "BitwiseXor", "IBitwiseOperators<TElement, TElement, TElement>");
			WriteUnaryNumericHelperMethod(writer, "CtPop", "unmanaged");

			foreach ((string llvmName, string? dotNetName, string requiredInterfaces) in UnaryInterfaceOperations)
			{
				WriteUnaryTensorPrimitivesMethod(writer, llvmName, dotNetName, ReplaceTypeParameterName(requiredInterfaces));
			}

			foreach ((string llvmName, string? dotNetName, string requiredInterfaces) in BinaryInterfaceOperations)
			{
				WriteBinaryTensorPrimitivesMethod(writer, llvmName, dotNetName, ReplaceTypeParameterName(requiredInterfaces));
			}
		}

		return stringWriter.ToString();

		static void WriteUnaryTensorPrimitivesMethod(IndentedTextWriter writer, string llvmName, string? dotNetName, string requiredInterfaces)
		{
			writer.WriteLineNoTabs();
			writer.WriteLine($"public static TBuffer {llvmName}<TBuffer, TElement>(TBuffer x)");
			using (new Indented(writer))
			{
				writer.WriteLine("where TBuffer : struct, IInlineArray<TElement>");
				writer.WriteLine($"where TElement : {requiredInterfaces}");
			}
			using (new CurlyBrackets(writer))
			{
				writer.WriteLine("TBuffer result = default;");
				writer.WriteLine($"TensorPrimitives.{(string.IsNullOrEmpty(dotNetName) ? llvmName : dotNetName)}(x.AsReadOnlySpan<TBuffer, TElement>(), result.AsSpan<TBuffer, TElement>());");
				writer.WriteLine("return result;");
			}
		}

		static void WriteUnaryNumericHelperMethod(IndentedTextWriter writer, string methodName, string requiredInterfaces)
		{
			writer.WriteLineNoTabs();
			writer.WriteLine($"public static TBuffer {methodName}<TBuffer, TElement>(TBuffer x, TBuffer y)");
			using (new Indented(writer))
			{
				writer.WriteLine("where TBuffer : struct, IInlineArray<TElement>");
				writer.WriteLine($"where TElement : {requiredInterfaces}");
			}
			using (new CurlyBrackets(writer))
			{
				writer.WriteLine("TBuffer result = default;");
				using (new For(writer, "int i = 0", "i < TBuffer.Length", "i++"))
				{
					writer.WriteLine($"result.SetElement(i, NumericHelper.{methodName}<TElement>(x.GetElement<TBuffer, TElement>(i)));");
				}
				writer.WriteLine("return result;");
			}
		}

		static void WriteBinaryTensorPrimitivesMethod(IndentedTextWriter writer, string llvmName, string? dotNetName, string requiredInterfaces)
		{
			writer.WriteLineNoTabs();
			writer.WriteLine($"public static TBuffer {llvmName}<TBuffer, TElement>(TBuffer x, TBuffer y)");
			using (new Indented(writer))
			{
				writer.WriteLine("where TBuffer : struct, IInlineArray<TElement>");
				writer.WriteLine($"where TElement : {requiredInterfaces}");
			}
			using (new CurlyBrackets(writer))
			{
				writer.WriteLine("TBuffer result = default;");
				writer.WriteLine($"TensorPrimitives.{(string.IsNullOrEmpty(dotNetName) ? llvmName : dotNetName)}(x.AsReadOnlySpan<TBuffer, TElement>(), y.AsReadOnlySpan<TBuffer, TElement>(), result.AsSpan<TBuffer, TElement>());");
				writer.WriteLine("return result;");
			}
		}

		static void WriteBinaryNumericHelperMethod(IndentedTextWriter writer, string methodName, string requiredInterfaces)
		{
			writer.WriteLineNoTabs();
			writer.WriteLine($"public static TBuffer {methodName}<TBuffer, TElement>(TBuffer x, TBuffer y)");
			using (new Indented(writer))
			{
				writer.WriteLine("where TBuffer : struct, IInlineArray<TElement>");
				writer.WriteLine($"where TElement : {requiredInterfaces}");
			}
			using (new CurlyBrackets(writer))
			{
				writer.WriteLine("TBuffer result = default;");
				using (new For(writer, "int i = 0", "i < TBuffer.Length", "i++"))
				{
					writer.WriteLine($"result.SetElement(i, NumericHelper.{methodName}<TElement>(x.GetElement<TBuffer, TElement>(i), y.GetElement<TBuffer, TElement>(i)));");
				}
				writer.WriteLine("return result;");
			}
		}

		static string ReplaceTypeParameterName(string requiredInterfaces)
		{
			return requiredInterfaces
				.Replace("<T>", "<TElement>")
				.Replace("<T, T>", "<TElement, TElement>")
				.Replace("<T, T, T>", "<TElement, TElement, TElement>");
		}
	}
}
