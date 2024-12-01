using Google.OrTools.Sat;

namespace AssetRipper.Translation.Cpp;

internal static class BoundedLinearExpressionExtensions
{
	public static BoundedLinearExpression Not(this BoundedLinearExpression expression)
	{
		return expression.CtType switch
		{
			BoundedLinearExpression.Type.VarEqVar => new BoundedLinearExpression(expression.Left, expression.Right, false),
			BoundedLinearExpression.Type.VarDiffVar => new BoundedLinearExpression(expression.Left, expression.Right, true),
			BoundedLinearExpression.Type.VarEqCst => new BoundedLinearExpression(expression.Left, expression.Lb, false),
			BoundedLinearExpression.Type.VarDiffCst => new BoundedLinearExpression(expression.Left, expression.Lb, true),
			_ => throw new ArgumentOutOfRangeException(nameof(expression)),
		};
	}

	public static BoolVar ToBoolean(this BoundedLinearExpression expression, CpModel model)
	{
		BoolVar boolean = model.NewBoolVar(nameof(ToBoolean));
		model.Add(expression).OnlyEnforceIf(boolean);
		model.Add(expression.Not()).OnlyEnforceIf(boolean.Not());
		return boolean;
	}

	public static ILiteral ToLiteral(this BoundedLinearExpression expression, CpModel model)
	{
		return expression.ToBoolean(model);
	}
}
