namespace AssetRipper.Translation.Cpp.ExceptionHandling;

internal class LiftedSeseRegion(ISeseRegion original) : ModifiableSeseRegion, ISeseRegionAlias
{
	public ISeseRegion Original { get; } = original;
	public override SeseRegionType Type => Original.Type;
	public override bool IsFunctionEntrypoint => Original.IsFunctionEntrypoint;
	public override string ToString()
	{
		return Original.ToString() ?? nameof(LiftedSeseRegion);
	}
	/// <summary>
	/// For the debugger
	/// </summary>
	private ISeseRegion UltimateOriginal
	{
		get
		{
			ISeseRegion original = Original;
			while (original is ISeseRegionAlias alias)
			{
				original = alias.Original;
			}
			return original;
		}
	}
}
