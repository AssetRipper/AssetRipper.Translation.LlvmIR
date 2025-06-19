using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class ParseTreeExtensions
{
	public static string GetText(this IParseTree tree, string input)
	{
		if (tree is ParserRuleContext { ChildCount: > 0 } ruleContext)
		{
			return input.Substring(ruleContext.Start.StartIndex, ruleContext.Stop.StopIndex - ruleContext.Start.StartIndex + 1);
		}
		else
		{
			return tree.GetText();
		}
	}
}
