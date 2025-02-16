using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.Cpp;

internal sealed class InstructionNotSupportedException : NotSupportedException
{
	public string OpCode { get; }
	public string Line { get; }

	public InstructionNotSupportedException(string code, string line)
	{
		OpCode = code;
		Line = line;
	}

	public override string Message => $"Instruction with op code {OpCode} is not supported.";

	[DebuggerHidden]
	[StackTraceHidden]
	[DoesNotReturn]
	public static void Throw(string code, string line)
	{
		throw new InstructionNotSupportedException(code, line);
	}

	[DebuggerHidden]
	[StackTraceHidden]
	[DoesNotReturn]
	internal static unsafe void* ThrowPointer(string code, string line)
	{
		Throw(code, line);
		return null;
	}

	[DebuggerHidden]
	[StackTraceHidden]
	[DoesNotReturn]
	internal static T ThrowStruct<T>(string code, string line)
	{
		Throw(code, line);
		return default;
	}
}
