using System.Diagnostics;
using System.IO.Hashing;
using System.Text.Json;

namespace AssetRipper.Translation.Cpp.SampleGenerator;

internal static class Program
{
	private sealed record class HashFile(string ClangVersionInfo, Dictionary<string, uint> Hashes);

	static void Main()
	{
		string clangVersionInfo = GetClangVersionInfo();
		Dictionary<string, uint> hashes;
		const string PathToHashes = "hashes.json";
		if (File.Exists(PathToHashes))
		{
			string json = File.ReadAllText(PathToHashes);
			HashFile? hashFile = JsonSerializer.Deserialize<HashFile>(json);
			if (hashFile is null or { Hashes: null } || hashFile.ClangVersionInfo != clangVersionInfo)
			{
				hashes = [];
			}
			else
			{
				hashes = hashFile.Hashes;
				foreach (string path in hashes.Keys.ToArray())
				{
					if (!File.Exists(path))
					{
						hashes.Remove(path);
					}
				}
			}
		}
		else
		{
			hashes = [];
		}

		const string PathToSamples = "../../../../Samples";
		foreach (string file in Directory.EnumerateFiles(PathToSamples, "*.cpp", SearchOption.TopDirectoryOnly))
		{
			uint hash = ComputeHash(file);
			string ir_path = Path.ChangeExtension(file, ".ll");
			if (!hashes.TryGetValue(file, out uint old_hash) || old_hash != hash || !File.Exists(ir_path))
			{
				Console.WriteLine($"Processing {file}");

				GenerateIR(file, ir_path);

				hashes[file] = hash;
			}
		}

		File.WriteAllText(PathToHashes, JsonSerializer.Serialize(new HashFile(clangVersionInfo, hashes)));

		Console.WriteLine("Done!");
	}

	private static uint ComputeHash(string path)
	{
		return Crc32.HashToUInt32(File.ReadAllBytes(path));
	}

	private static void GenerateIR(string inputFile, string outputFile)
	{
		// Prepare the Clang command to generate IR
		string clangCommand = $"clang -S -emit-llvm -o {outputFile} {inputFile}";

		// Execute the Clang command
		ProcessStartInfo processInfo = new("cmd.exe", $"/c {clangCommand}")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using (Process process = new())
		{
			process.StartInfo = processInfo;
			process.Start();

			process.WaitForExit();

			// Read and display the output from the command
			string output = process.StandardOutput.ReadToEnd();
			if (!string.IsNullOrEmpty(output))
			{
				Console.WriteLine(output);
			}

			string error = process.StandardError.ReadToEnd();
			if (!string.IsNullOrEmpty(error))
			{
				Console.WriteLine(error);
			}
		}

		if (!File.Exists(outputFile))
		{
			throw new FileNotFoundException($"Failed to generate IR for {inputFile}");
		}
		else
		{
			string[] lines = File.ReadAllLines(outputFile);
			if (lines.Length > 4
				&& lines[0].StartsWith("; ModuleID = ", StringComparison.Ordinal)
				&& lines[1].StartsWith("source_filename = ", StringComparison.Ordinal)
				&& lines[2].StartsWith("target datalayout = ", StringComparison.Ordinal)
				&& lines[3].StartsWith("target triple = ", StringComparison.Ordinal))
			{
				// Remove the target lines
				string sourceFilenameLine = $"source_filename = \"{Path.GetFileName(inputFile)}\"";
				string contents = string.Join('\n', lines.Skip(4).Prepend(sourceFilenameLine)) + '\n';
				File.WriteAllText(outputFile, contents);
			}
		}
	}

	private static string GetClangVersionInfo()
	{
		ProcessStartInfo processInfo = new("clang", "--version")
		{
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		using (Process process = new())
		{
			process.StartInfo = processInfo;
			process.Start();
			process.WaitForExit();
			return process.StandardOutput.ReadToEnd();
		}
	}
}
