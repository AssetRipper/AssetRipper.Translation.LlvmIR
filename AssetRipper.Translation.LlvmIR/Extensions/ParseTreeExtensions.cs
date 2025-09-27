using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class ParseTreeExtensions
{
	[return: NotNullIfNotNull(nameof(tree))]
	public static string? GetText(this IParseTree? tree, string input)
	{
		if (tree is ParserRuleContext { ChildCount: > 0 } ruleContext)
		{
			return input.Substring(ruleContext.Start.StartIndex, ruleContext.Stop.StopIndex - ruleContext.Start.StartIndex + 1);
		}
		else if (tree is ITerminalNode terminalNode)
		{
			return input.Substring(terminalNode.Symbol.StartIndex, terminalNode.Symbol.StopIndex - terminalNode.Symbol.StartIndex + 1);
		}
		else if (tree is null)
		{
			return null;
		}
		else
		{
			return tree.GetText();
		}
	}

	public static int GetStart(this IParseTree? tree)
	{
		if (tree is ParserRuleContext { ChildCount: > 0 } ruleContext)
		{
			return ruleContext.Start.StartIndex;
		}
		else if (tree is ITerminalNode terminalNode)
		{
			return terminalNode.Symbol.StartIndex;
		}
		else
		{
			return -1;
		}
	}

	public static int GetStop(this IParseTree? tree)
	{
		if (tree is ParserRuleContext { ChildCount: > 0 } ruleContext)
		{
			return ruleContext.Stop.StopIndex;
		}
		else if (tree is ITerminalNode terminalNode)
		{
			return terminalNode.Symbol.StopIndex;
		}
		else
		{
			return -1;
		}
	}
}
