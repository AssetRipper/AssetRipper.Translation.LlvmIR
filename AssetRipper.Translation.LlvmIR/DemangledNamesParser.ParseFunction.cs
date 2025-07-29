using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using AssetRipper.Translation.LlvmIR.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.LlvmIR;

public partial class DemangledNamesParser
{
	public static IParseTree ParseFunction(string input)
	{
		ICharStream stream = CharStreams.fromString(input);
		ITokenSource lexer = new DemangledNamesLexer(stream, TextWriter.Null, TextWriter.Null);
		ITokenStream tokens = new CommonTokenStream(lexer);
		DemangledNamesParser parser = new(tokens, TextWriter.Null, TextWriter.Null);
		return parser.function();
	}

	public static bool ParseFunction(
		string input,
		out string? returnType,
		out string? @namespace,
		out string? typeName,
		[NotNullWhen(true)] out string? functionIdentifier,
		[NotNullWhen(true)] out string? functionName,
		[NotNullWhen(true)] out string[]? templateParameters,
		[NotNullWhen(true)] out string[]? normalParameters)
	{
		IParseTree tree = ParseFunction(input);

		if (ErrorListener.HasErrors(tree) || tree.ChildCount == 0 || (tree as ParserRuleContext)?.exception is not null)
		{
			returnType = null;
			@namespace = null;
			typeName = null;
			functionIdentifier = null;
			functionName = null;
			templateParameters = null;
			normalParameters = null;
			return false;
		}

		try
		{
			returnType = tree.GetChild(1).GetText(input).ToNullIfEmpty();
			IParseTree declaringScope = tree.GetChild(3);
			if (declaringScope.ChildCount == 0)
			{
				@namespace = null;
				typeName = null;
			}
			else if (declaringScope.GetChild(0).ChildCount > 2)
			{
				@namespace = declaringScope.GetChild(0).GetChild(0).GetText(input);
				typeName = declaringScope.GetChild(0).GetChild(3).GetText(input);
			}
			else
			{
				@namespace = null;
				typeName = declaringScope.GetChild(0).GetText(input);
			}
			functionIdentifier = tree.GetChild(4).GetChild(0).GetText(input);
			functionName = tree.GetChild(4).GetChild(0).GetText(input) + tree.GetChild(4).GetChild(1).GetText(input);
			templateParameters = tree.GetChild(4).GetChild(1).GetText(input).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			normalParameters = tree.GetChild(6).GetText(input).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			if (normalParameters.Length == 1 && normalParameters[0] == "void")
			{
				normalParameters = [];
			}

			return true;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception);
			returnType = null;
			@namespace = null;
			typeName = null;
			functionIdentifier = null;
			functionName = null;
			templateParameters = null;
			normalParameters = null;
			return false;
		}
	}

	private sealed class ErrorListener : IParseTreeListener
	{
		public int ErrorCount { get; private set; }
		void IParseTreeListener.EnterEveryRule(ParserRuleContext ctx)
		{
		}

		void IParseTreeListener.ExitEveryRule(ParserRuleContext ctx)
		{
		}

		void IParseTreeListener.VisitErrorNode(IErrorNode node)
		{
			ErrorCount++;
		}

		void IParseTreeListener.VisitTerminal(ITerminalNode node)
		{
		}

		public static bool HasErrors(IParseTree tree)
		{
			ErrorListener errorListener = new();
			ParseTreeWalker.Default.Walk(errorListener, tree);
			return errorListener.ErrorCount > 0;
		}
	}
}
