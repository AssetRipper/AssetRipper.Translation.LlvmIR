using Google.OrTools.Sat;

namespace AssetRipper.Translation.Cpp.Extensions;

internal static class BoolVarExtensions
{
	public static BoolVar BooleanAnd(this CpModel model, BoolVar left, BoolVar right)
	{
		BoolVar boolean = model.NewBoolVar(nameof(BooleanAnd));
		model.AddBoolAnd([left, right]).OnlyEnforceIf(boolean);
		model.AddBoolOr([left.Not(), right.Not()]).OnlyEnforceIf(boolean.Not());
		return boolean;
	}

	public static BoolVar BooleanAnd(this CpModel model, IEnumerable<BoolVar> booleans)
	{
		BoolVar boolean = model.NewBoolVar(nameof(BooleanAnd));
		model.AddBoolAnd(booleans).OnlyEnforceIf(boolean);
		model.AddBoolOr(booleans.Select(t => t.Not())).OnlyEnforceIf(boolean.Not());
		return boolean;
	}

	public static BoolVar BooleanOr(this CpModel model, BoolVar left, BoolVar right)
	{
		BoolVar boolean = model.NewBoolVar(nameof(BooleanOr));
		model.AddBoolOr([left, right]).OnlyEnforceIf(boolean);
		model.AddBoolAnd([left.Not(), right.Not()]).OnlyEnforceIf(boolean.Not());
		return boolean;
	}

	public static BoolVar BooleanOr(this CpModel model, IEnumerable<BoolVar> booleans)
	{
		BoolVar boolean = model.NewBoolVar(nameof(BooleanOr));
		model.AddBoolOr(booleans).OnlyEnforceIf(boolean);
		model.AddBoolAnd(booleans.Select(t => t.Not())).OnlyEnforceIf(boolean.Not());
		return boolean;
	}

	public static BoolVar BooleanXor(this CpModel model, BoolVar left, BoolVar right)
	{
		BoolVar boolean = model.NewBoolVar(nameof(BooleanXor));
		model.AddBoolXor([left, right]).OnlyEnforceIf(boolean);
		model.AddBoolXor([left.Not(), right.Not()]).OnlyEnforceIf(boolean.Not());
		return boolean;
	}

	public static BoolVar BooleanXor(this CpModel model, IEnumerable<BoolVar> booleans)
	{
		BoolVar boolean = model.NewBoolVar(nameof(BooleanXor));
		model.AddBoolXor(booleans).OnlyEnforceIf(boolean);
		model.AddBoolXor(booleans.Select(t => t.Not())).OnlyEnforceIf(boolean.Not());
		return boolean;
	}
}
