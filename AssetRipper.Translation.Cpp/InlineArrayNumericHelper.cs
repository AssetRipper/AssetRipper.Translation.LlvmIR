using System.Numerics;
using System.Numerics.Tensors;

namespace AssetRipper.Translation.Cpp;

internal static partial class InlineArrayNumericHelper
{
	public static TBuffer Negate<TBuffer, TElement>(TBuffer x)
		where TBuffer : struct, IInlineArray<TElement>
		where TElement : IUnaryNegationOperators<TElement, TElement>
	{
		TBuffer result = default;
		TensorPrimitives.Negate(x.AsReadOnlySpan<TBuffer, TElement>(), result.AsSpan<TBuffer, TElement>());
		return result;
	}
}
