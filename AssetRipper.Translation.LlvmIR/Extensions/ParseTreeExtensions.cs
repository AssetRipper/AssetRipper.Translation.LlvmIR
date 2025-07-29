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
		else if (tree is null)
		{
			return null;
		}
		else
		{
			return tree.GetText();
		}
	}
}
