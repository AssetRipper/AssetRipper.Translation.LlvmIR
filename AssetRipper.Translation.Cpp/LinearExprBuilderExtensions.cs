using Google.OrTools.Sat;

namespace AssetRipper.Translation.Cpp;

internal static class LinearExprBuilderExtensions
{
	public static void AddCondtional(this LinearExprBuilder builder, BoolVar condition, long trueValue)
	{
		builder.Add(condition * trueValue);
	}

	public static void AddCondtional(this LinearExprBuilder builder, BoolVar condition, long trueValue, long falseValue)
	{
		builder.Add(condition * trueValue + condition.NotAsExpr() * falseValue);
	}
}
