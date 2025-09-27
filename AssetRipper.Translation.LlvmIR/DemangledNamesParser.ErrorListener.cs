using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace AssetRipper.Translation.LlvmIR;

public partial class DemangledNamesParser
{
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
