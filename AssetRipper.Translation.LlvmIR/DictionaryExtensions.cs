namespace AssetRipper.Translation.LlvmIR;

internal static class DictionaryExtensions
{
	public static TValue? TryGetValue<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull
	{
		return dictionary.TryGetValue(key, out TValue? value) ? value : default;
	}
}
