namespace AssetRipper.Translation.Cpp.ExceptionHandling;

internal static class ListExtensions
{
	extension<T>(List<T> list)
	{
		public bool TryAdd(T item)
		{
			if (!list.Contains(item))
			{
				list.Add(item);
				return true;
			}
			return false;
		}
	}
}
