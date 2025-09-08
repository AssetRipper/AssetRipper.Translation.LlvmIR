using System.Collections;

namespace AssetRipper.Translation.LlvmIR;

internal struct InlineArrayBuilder<TBuffer, TElement> : IEnumerable<TElement>
	where TBuffer : struct, IInlineArray<TElement>
{
	private int _index;
	private TBuffer _buffer;

	public void Add(TElement element)
	{
		if (_index >= TBuffer.Length)
		{
			throw new InvalidOperationException($"Cannot add more than {TBuffer.Length} elements to the inline array.");
		}
		_buffer.SetElement(_index, element);
		_index++;
	}

	public IEnumerator<TElement> GetEnumerator()
	{
		for (int i = 0; i < _index; i++)
		{
			yield return _buffer.GetElement<TBuffer, TElement>(i);
		}
	}

	public readonly TBuffer ToInlineArray()
	{
		if (_index != TBuffer.Length)
		{
			throw new InvalidOperationException($"Expected {_index} elements, but got {TBuffer.Length}.");
		}
		return _buffer;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
