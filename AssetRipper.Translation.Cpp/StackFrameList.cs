namespace AssetRipper.Translation.Cpp;

internal struct StackFrameList
{
	[ThreadStatic]
	public static StackFrameList Current;

	private List<StackFrame>? Frames;

	public StackFrame New<T>() where T : unmanaged
	{
		Frames ??= new();
		StackFrame frame = StackFrame.Create<T>(Frames.Count);
		Frames.Add(frame);
		return frame;
	}

	public static void ExitToUserCode()
	{
		if (ExceptionInfo.Current is null)
		{
			Current.Clear();
		}
		else
		{
			string? message = ExceptionInfo.Current.GetMessage();
			ExceptionInfo.Current = null;
			Current.Clear();
			throw new Exception(message);
		}
	}

	internal readonly void Clear()
	{
		if (Frames != null)
		{
			foreach (StackFrame frame in Frames)
			{
				frame.FreeLocals();
			}
			Frames.Clear();
		}
	}

	internal readonly void Clear(int startIndex)
	{
		for (int i = Frames!.Count - 1; i >= startIndex; i--)
		{
			Frames[i].FreeLocals();
		}
		Frames.RemoveRange(startIndex, Frames.Count - startIndex);
	}

	public readonly void Clear(StackFrame startFrame) => Clear(startFrame.Index);
}
