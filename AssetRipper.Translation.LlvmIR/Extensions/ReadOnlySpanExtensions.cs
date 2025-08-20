using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class ReadOnlySpanExtensions
{
	public static bool TryParseCharacterArray(this ReadOnlySpan<byte> data, [NotNullWhen(true)] out string? value)
	{
		if (data.Length % sizeof(char) != 0)
		{
			value = null;
			return false;
		}

		char[] chars = new char[data.Length / sizeof(char)];
		for (int i = 0; i < chars.Length; i++)
		{
			char c = (char)BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(i * sizeof(char)));
			if (!char.IsAscii(c))
			{
				value = null;
				return false;
			}
			if (char.IsControl(c) && (i != chars.Length - 1 || c != '\0')) // Allow null terminator
			{
				value = null;
				return false;
			}
			chars[i] = c;
		}
		value = new string(chars);
		return true;
	}
}
