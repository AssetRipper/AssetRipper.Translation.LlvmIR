using System.Collections;

namespace AssetRipper.Translation.LlvmIR;

internal interface IInlineArray<out T>
{
	static abstract int Length { get; }
}
internal interface IInlineArray<TSelf, out TElement> : IInlineArray<TElement>, IReadOnlyList<TElement>
	where TSelf : struct, IInlineArray<TElement>
{
	TElement IReadOnlyList<TElement>.this[int index]
	{
		get
		{
			TSelf temp = (TSelf)this;
			return InlineArrayHelper.GetElement<TSelf, TElement>(ref temp, index);
		}
	}
	int IReadOnlyCollection<TElement>.Count => TSelf.Length;
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<TElement>)this).GetEnumerator();
	IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
	{
		for (int i = 0; i < TSelf.Length; i++)
		{
			yield return ((IReadOnlyList<TElement>)this)[i];
		}
	}
}
