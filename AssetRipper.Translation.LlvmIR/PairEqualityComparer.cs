namespace AssetRipper.Translation.LlvmIR;

internal sealed record class PairEqualityComparer<T1, T2>(IEqualityComparer<T1> Comparer1, IEqualityComparer<T2> Comparer2)
	: IEqualityComparer<(T1, T2)>
	where T1 : notnull
	where T2 : notnull
{
	public bool Equals((T1, T2) x, (T1, T2) y)
	{
		return Comparer1.Equals(x.Item1, y.Item1) && Comparer2.Equals(x.Item2, y.Item2);
	}

	public int GetHashCode((T1, T2) obj)
	{
		return HashCode.Combine(Comparer1.GetHashCode(obj.Item1), Comparer2.GetHashCode(obj.Item2));
	}
}

