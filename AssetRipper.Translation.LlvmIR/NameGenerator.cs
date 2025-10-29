using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetRipper.Translation.LlvmIR;

internal static partial class NameGenerator
{
	/// <summary>
	/// Not official
	/// </summary>
	/// <remarks>
	/// Characters chosen to avoid visually similar characters (l and 1, O and 0).
	/// </remarks>
	private const string Base32Characters = "abcdefghijkmnpqrstuvwxyz23456789";

	public static bool IsValidCSharpName(string name)
	{
		return !string.IsNullOrEmpty(name) && !char.IsDigit(name[0]) && !NonWordRegex.IsMatch(name);
	}

	public static string CleanName(string input, [ConstantExpected] string defaultName)
	{
		string onlyWordCharacters = NonWordRegex.Replace(input, "_");
		string uniformSpacing = string.Join('_', onlyWordCharacters.Split('_', StringSplitOptions.RemoveEmptyEntries));
		if (uniformSpacing.Length == 0)
		{
			return defaultName;
		}
		else if (char.IsDigit(uniformSpacing[0]))
		{
			return $"_{uniformSpacing}";
		}
		else
		{
			return uniformSpacing;
		}
	}

	public static string GenerateName(string cleanName, string name, int index = 0)
	{
		uint hash = Hash(name, index);
		Span<char> buffer =
		[
			Base32Characters[(int)(hash & 0x1F)],
			Base32Characters[(int)((hash >> 5) & 0x1F)],
			Base32Characters[(int)((hash >> 10) & 0x1F)],
			Base32Characters[(int)((hash >> 15) & 0x1F)],
			Base32Characters[(int)((hash >> 20) & 0x1F)],
			Base32Characters[(int)((hash >> 25) & 0x1F)],
		];
		return $"{cleanName}_{buffer}";

		static uint Hash(string str, int index)
		{
			int length = Encoding.UTF8.GetByteCount(str);
			byte[] bytes = ArrayPool<byte>.Shared.Rent(length);
			Span<byte> span = new(bytes, 0, length);
			Encoding.UTF8.GetBytes(str, span);
			uint hash = XxHash32.HashToUInt32(span, index);
			ArrayPool<byte>.Shared.Return(bytes);
			return hash;
		}
	}

	[GeneratedRegex(@"\W")]
	private static partial Regex NonWordRegex { get; }
}
