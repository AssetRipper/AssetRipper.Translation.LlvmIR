using System.Collections;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.ExceptionHandling;

[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(CombinedList<>.CombinedListDebugView))]
internal sealed record class CombinedList<T>(IReadOnlyList<T> a, IReadOnlyList<T> b) : IReadOnlyList<T> where T : class
{
	public T this[int index] => index < a.Count ? a[index] : b[index - a.Count];
	public int Count => a.Count + b.Count;
	public IEnumerator<T> GetEnumerator() => new CombinedEnumerator(this);
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public CombinedList(IReadOnlyList<T> a, IReadOnlyList<T> b, IReadOnlyList<T> c) : this(new CombinedList<T>(a, b), c)
	{
	}

	private sealed class CombinedEnumerator(CombinedList<T> list) : IEnumerator<T>
	{
		private int _index = -1;
		public T Current => list[_index];

		object IEnumerator.Current => Current;

		public void Dispose()
		{
			_index = list.Count;
		}

		public bool MoveNext()
		{
			_index++;
			return _index < list.Count;
		}

		public void Reset()
		{
			_index = -1;
		}
	}

	private sealed class CombinedListDebugView(CombinedList<T> list)
	{
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public T[] Items
		{
			get
			{
				T[] items = new T[list.Count];
				for (int i = 0; i < list.Count; i++)
				{
					items[i] = list[i];
				}
				return items;
			}
		}
	}
}
