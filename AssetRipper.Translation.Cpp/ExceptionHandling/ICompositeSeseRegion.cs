namespace AssetRipper.Translation.Cpp.ExceptionHandling;

/// <summary>
/// A <see cref="ISeseRegion{T}"/> that contains other <see cref="ISeseRegion{T}"/>s.
/// </summary>
public interface ICompositeSeseRegion : ISeseRegion
{
	IReadOnlyList<ISeseRegion> Children { get; }
}
