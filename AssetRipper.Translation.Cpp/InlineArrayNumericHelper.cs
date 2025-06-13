using System.Numerics;
using System.Numerics.Tensors;

namespace AssetRipper.Translation.Cpp;

internal static partial class InlineArrayNumericHelper
{
	public static TBuffer Negate<TBuffer, TElement>(TBuffer x)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : INumberBase<TElement>
	{
		TBuffer result = default;
		for (int i = 0; i < TBuffer.Length; i++)
		{
			result.SetElement(i, NumericHelper.Negate(x.GetElement<TBuffer, TElement>(i)));
		}
		return result;
	}
}
