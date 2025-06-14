using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetRipper.Translation.Cpp;

internal static partial class NameGenerator
{
	/// <summary>
	/// Not official
	/// </summary>
	private const string Base32Characters = "abcdefghijklmnopqrstuvwxyz234579";

	/// <summary>
	/// Modified: replaced + and / with Σ (sigma) and Ω (omega).
	/// </summary>
	private const string Base64Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789ΣΩ";

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

	public static string GenerateName(string cleanName, string name)
	{
		uint hash = Crc32.HashToUInt32(Encoding.UTF8.GetBytes(name));
		Span<char> buffer = stackalloc char[7];
		WriteBase32(hash, buffer);
		return $"{cleanName}_{buffer}";
	}

	private static void WriteBase32(uint value, Span<char> buffer)
	{
		buffer[0] = Base32Characters[(int)(value & 0x1F)];
		buffer[1] = Base32Characters[(int)((value >> 5) & 0x1F)];
		buffer[2] = Base32Characters[(int)((value >> 10) & 0x1F)];
		buffer[3] = Base32Characters[(int)((value >> 15) & 0x1F)];
		buffer[4] = Base32Characters[(int)((value >> 20) & 0x1F)];
		buffer[5] = Base32Characters[(int)((value >> 25) & 0x1F)];
		buffer[6] = Base32Characters[(int)((value >> 30) & 0x1F)];
	}

	private static void WriteBase64(uint value, Span<char> buffer)
	{
		buffer[0] = Base64Characters[(int)(value & 0x3F)];
		buffer[1] = Base64Characters[(int)((value >> 6) & 0x3F)];
		buffer[2] = Base64Characters[(int)((value >> 12) & 0x3F)];
		buffer[3] = Base64Characters[(int)((value >> 18) & 0x3F)];
		buffer[4] = Base64Characters[(int)((value >> 24) & 0x3F)];
		buffer[5] = Base64Characters[(int)((value >> 30) & 0x3F)];
	}

	[GeneratedRegex(@"\W")]
	private static partial Regex NonWordRegex { get; }
}
