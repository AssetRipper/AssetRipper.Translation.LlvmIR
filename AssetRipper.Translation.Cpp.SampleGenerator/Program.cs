using System.Diagnostics;
using System.IO.Hashing;
using System.Text.Json;

namespace AssetRipper.Translation.Cpp.SampleGenerator;

internal static class Program
{
	static void Main()
	{
		Dictionary<string, uint> hashes;
		const string PathToHashes = "hashes.json";
		if (File.Exists(PathToHashes))
		{
			string json = File.ReadAllText(PathToHashes);
			hashes = JsonSerializer.Deserialize<Dictionary<string, uint>>(json) ?? [];
			foreach (string path in hashes.Keys.ToArray())
			{
				if (!File.Exists(path))
				{
					hashes.Remove(path);
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
			uint hash = HashFile(file);
			string ir_path = Path.ChangeExtension(file, ".ll");
			if (!hashes.TryGetValue(file, out uint old_hash) || old_hash != hash || !File.Exists(ir_path))
			{
				Console.WriteLine($"Processing {file}");

				GenerateIR(file, ir_path);

				hashes[file] = hash;
			}
		}

		File.WriteAllText(PathToHashes, JsonSerializer.Serialize(hashes));

		Console.WriteLine("Done!");
	}

	private static uint HashFile(string path)
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
	}
}
