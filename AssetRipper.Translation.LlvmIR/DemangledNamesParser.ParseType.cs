using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using AssetRipper.Translation.LlvmIR.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.LlvmIR;

public partial class DemangledNamesParser
{
	public static IParseTree ParseType(string input)
	{
		ICharStream stream = CharStreams.fromString(input);
		ITokenSource lexer = new DemangledNamesLexer(stream, TextWriter.Null, TextWriter.Null);
		ITokenStream tokens = new CommonTokenStream(lexer);
		DemangledNamesParser parser = new(tokens, TextWriter.Null, TextWriter.Null);
		return parser.type();
	}

	public static bool ParseType(
		string input,
		[NotNullWhen(true)] out string? cleanType)
	{
		IParseTree tree = ParseType(input);

		if (ErrorListener.HasErrors(tree) || tree.ChildCount == 0 || tree is ParserRuleContext { exception: not null })
		{
			Console.Error.WriteLine("Could not parse:\n" + input);
			cleanType = null;
			return false;
		}

		try
		{
			return TryFormatType((TypeContext)tree, input, out cleanType);
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception);
			cleanType = null;
			return false;
		}

		static bool TryFormatType(TypeContext context, string input, [NotNullWhen(true)] out string? cleanType)
		{
			if (context.ChildCount == 0)
			{
				cleanType = null;
				return false;
			}

			IParseTree firstChild = context.GetChild(0);
			if (firstChild is QualifiedTypeIdentifierContext qualifiedTypeIdentifierContext)
			{
				if (!TryFormatQualifiedTypeIdentifier(qualifiedTypeIdentifierContext, input, out string? qualifiedString))
				{
					cleanType = null;
					return false;
				}
				string suffix = "";
				if (context.ChildCount > 1)
				{
					int startIndex = context.GetChild(1).GetStart();
					int stopIndex = context.GetChild(context.ChildCount - 1).GetStop();
					if (!IsValidStartStop(startIndex, stopIndex, input))
					{
						cleanType = null;
						return false;
					}
					suffix = input.Substring(startIndex, stopIndex - startIndex + 1);
				}
				cleanType = qualifiedString + suffix;
				return true;
			}
			else if (firstChild is TypeContext childContext)
			{
				if (!TryFormatType(childContext, input, out string? childString))
				{
					cleanType = null;
					return false;
				}

				string suffix = "";
				if (context.ChildCount > 1)
				{
					int startIndex = context.GetChild(1).GetStart();
					int stopIndex = context.GetChild(context.ChildCount - 1).GetStop();
					if (!IsValidStartStop(startIndex, stopIndex, input))
					{
						cleanType = null;
						return false;
					}
					suffix = input.Substring(startIndex, stopIndex - startIndex + 1);
				}

				cleanType = childString + suffix;
				return true;
			}
			else if (context.ChildCount > 1)
			{
				QualifiedTypeIdentifierContext secondChild = (QualifiedTypeIdentifierContext)context.GetChild(1);

				if (!TryFormatQualifiedTypeIdentifier(secondChild, input, out string? childString))
				{
					cleanType = null;
					return false;
				}

				string suffix = "";
				if (context.ChildCount > 2)
				{
					int startIndex = context.GetChild(2).GetStart();
					int stopIndex = context.GetChild(context.ChildCount - 1).GetStop();
					if (!IsValidStartStop(startIndex, stopIndex, input))
					{
						cleanType = null;
						return false;
					}
					suffix = input.Substring(startIndex, stopIndex - startIndex + 1);
				}

				cleanType = childString + suffix;
				return true;
			}
			else
			{
				cleanType = null;
				return false;
			}
		}

		static bool TryFormatQualifiedTypeIdentifier(QualifiedTypeIdentifierContext context, string input, [NotNullWhen(true)] out string? cleanType)
		{
			if (context.ChildCount == 0)
			{
				cleanType = null;
				return false;
			}

			IParseTree firstChild = context.GetChild(0);
			if (firstChild is TypeIdentifierContext typeIdentifierContext)
			{
				return TryFormatTypeIdentifier(typeIdentifierContext, input, out cleanType);
			}
			else if (firstChild is QualifiedTypeIdentifierContext childContext)
			{
				if (context.ChildCount != 4)
				{
					cleanType = null;
					return false;
				}

				if (!TryFormatQualifiedTypeIdentifier(childContext, input, out string? declaringScope))
				{
					cleanType = null;
					return false;
				}

				IParseTree lastChild = context.GetChild(3);
				if (lastChild is not TypeIdentifierContext identifierContext || !TryFormatTypeIdentifier(identifierContext, input, out string? identifier))
				{
					cleanType = null;
					return false;
				}

				cleanType = $"{declaringScope}::{identifier}";
				return true;
			}
			else
			{
				cleanType = null;
				return false;
			}
		}
		static bool TryFormatTypeIdentifier(TypeIdentifierContext context, string input, [NotNullWhen(true)] out string? cleanType)
		{
			if (context.ChildCount == 0)
			{
				cleanType = null;
				return false;
			}

			IParseTree lastChild = context.GetChild(context.ChildCount - 1);
			if (lastChild is TemplateContext template)
			{
				if (context.ChildCount == 1)
				{
					cleanType = null;
					return false;
				}

				int prefixStart = context.GetChild(0).GetStart();
				int prefixStop = context.GetChild(context.ChildCount - 2).GetStop();
				if (!IsValidStartStop(prefixStart, prefixStop, input))
				{
					cleanType = null;
					return false;
				}

				if (!TryFormatTemplate(template, input, out string? templateString))
				{
					cleanType = null;
					return false;
				}

				ReadOnlySpan<char> prefix = input.AsSpan(prefixStart, prefixStop - prefixStart + 1);

				cleanType = $"{prefix}{templateString}";
				return true;
			}
			else
			{
				cleanType = context.GetText(input);
				return true;
			}
		}
		static bool TryFormatTemplate(TemplateContext context, string input, [NotNullWhen(true)] out string? cleanType)
		{
			if (context.ChildCount == 0)
			{
				// Empty template
				cleanType = "";
				return true;
			}

			TemplateParameterContext[] parameterContexts = context.templateParameter();
			string[] parameterStrings = new string[parameterContexts.Length];
			for (int i = 0; i < parameterContexts.Length; i++)
			{
				if (!TryFormatTemplateParameter(parameterContexts[i], input, out string? parameterString))
				{
					cleanType = null;
					return false;
				}
				parameterStrings[i] = parameterString;
			}

			cleanType = $"<{string.Join(", ", parameterStrings)}>";
			return true;
		}
		static bool TryFormatTemplateParameter(TemplateParameterContext context, string input, [NotNullWhen(true)] out string? cleanType)
		{
			if (context.ChildCount > 0 && context.GetChild(0) is TypeContext childContext)
			{
				if (!TryFormatType(childContext, input, out string? childString))
				{
					cleanType = null;
					return false;
				}

				string suffix = "";
				if (context.ChildCount > 1)
				{
					int startIndex = context.GetChild(1).GetStart();
					int stopIndex = context.GetChild(context.ChildCount - 1).GetStop();
					if (!IsValidStartStop(startIndex, stopIndex, input))
					{
						cleanType = null;
						return false;
					}
					suffix = input.Substring(startIndex, stopIndex - startIndex + 1);
				}

				cleanType = childString + suffix;
				return true;
			}
			else
			{
				cleanType = context.GetText(input);
				return true;
			}
		}
		static bool IsValidStartStop(int startIndex, int stopIndex, string input)
		{
			return startIndex >= 0 && stopIndex >= 0 && stopIndex >= startIndex && stopIndex < input.Length;
		}
	}
}
