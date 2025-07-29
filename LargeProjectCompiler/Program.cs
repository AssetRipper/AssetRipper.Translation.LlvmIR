using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;

namespace LargeProjectCompiler;

internal static class Program
{
	static void Main(string[] args)
	{
		Arguments? arguments = Arguments.Parse(args);
		if (arguments is null)
		{
			return;
		}

		if (!OperatingSystem.IsWindows())
		{
			Console.WriteLine("This is only supported on Windows.");
			return;
		}

		string compileDbPath = arguments.CompileCommandsPath;
		if (!File.Exists(compileDbPath))
		{
			Console.WriteLine("Missing compile_commands.json");
			return;
		}

		string llvmLinkPath;
		if (string.IsNullOrEmpty(arguments.LlvmLink))
		{
			if (!LlvmLinkExists())
			{
				Console.Error.WriteLine("llvm-link not found. Please specify the path to llvm-link using --llvm-link.");
				return;
			}
			llvmLinkPath = "llvm-link";
		}
		else
		{
			if (!File.Exists(arguments.LlvmLink))
			{
				Console.Error.WriteLine($"Specified llvm-link path does not exist: {arguments.LlvmLink}");
				return;
			}
			llvmLinkPath = arguments.LlvmLink;
		}

		string outputPath;
		if (string.IsNullOrEmpty(arguments.Output))
		{
			outputPath = "final.bc";
		}
		else
		{
			outputPath = arguments.Output;
			string? directory = Path.GetDirectoryName(outputPath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}
		}

		string json = File.ReadAllText(compileDbPath);
		List<CompileCommand> entries = JsonSerializer.Deserialize<List<CompileCommand>>(json)!;

		List<string> bcFiles = new(entries.Count);

		string environmentDirectory = Environment.CurrentDirectory;
		try
		{
			for (int i = 0; i < entries.Count; i++)
			{
				CompileCommand entry = entries[i];

				List<string> commandParts = CommandParser.ParseCommand(entry.Command);

				if (commandParts.Count is 0 || !commandParts[0].Contains("CLANG_~1", StringComparison.Ordinal))
				{
					continue; // Skip if not a clang-cl command
				}

				if (!arguments.ShouldInclude(entry.File))
				{
					continue;
				}

				Environment.CurrentDirectory = entry.Directory;

				string bcOutputPath = Path.ChangeExtension(entry.Output, ".bc");

				Directory.CreateDirectory(Path.GetDirectoryName(bcOutputPath)!);
				RewriteCommand(commandParts, bcOutputPath, entry.Output);

				Console.WriteLine($">> Compiling {i + 1}/{entries.Count} {Path.GetFileName(entry.File)} -> {bcOutputPath}");
				bool success = RunCommand(commandParts, entry.Directory);
				if (!success)
				{
					Console.Error.WriteLine($"Failed to compile {entry.File}");
					return;
				}
				if (!File.Exists(bcOutputPath))
				{
					Console.Error.WriteLine($"Expected output file {bcOutputPath} does not exist after compilation.");
					return;
				}
				if (HasMain(arguments.MainFunctionDetector, bcOutputPath))
				{
					Console.Error.WriteLine($"Main function detected in {bcOutputPath}");
					return;
				}
				bcFiles.Add(Path.GetFullPath(bcOutputPath));
			}

			Environment.CurrentDirectory = environmentDirectory;

			Console.WriteLine($">> Linking all .bc files into {outputPath}");

			string tempFile = "temp_args.txt";
			CreateResponseFile(bcFiles, tempFile);
			try
			{
				bool linkSuccess = RunCommand([llvmLinkPath, $"@{tempFile}", "-o", outputPath], Directory.GetCurrentDirectory());
				if (!linkSuccess)
				{
					Console.Error.WriteLine("Linking failed.");
					return;
				}
				else
				{
					Console.WriteLine("Linking successful.");
				}
			}
			finally
			{
				if (File.Exists(tempFile))
				{
					Console.WriteLine($">> Removing {tempFile}");
					File.Delete(tempFile);
				}
			}
		}
		finally
		{
			foreach (string bcFile in bcFiles)
			{
				if (File.Exists(bcFile))
				{
					Console.WriteLine($">> Removing {bcFile}");
					File.Delete(bcFile);
				}
			}
		}
	}

	static void RewriteCommand(List<string> commandParts, string outputPath, string originalOutputPath)
	{
		if (commandParts.Count is 0)
		{
			throw new ArgumentException("There must be at least one command part", nameof(commandParts));
		}

		for (int i = commandParts.Count - 1; i >= 0; i--)
		{
			string part = commandParts[i];
			if (part is "-TP" or "-Zi" or "--" or "-MDd")
			{
				// Remove bad parts
				commandParts.RemoveAt(i);
			}
			else if (part is "-c")
			{
				// Only one -c is allowed, and we add it later.
				commandParts.RemoveAt(i);
			}
			else if (part.StartsWith("/Fo", StringComparison.Ordinal))
			{
				commandParts[i] = "-o";
				commandParts.Insert(i + 1, outputPath);
			}
			else if (part == originalOutputPath)
			{
				commandParts[i] = outputPath;
			}
			else if (part.StartsWith("-std:", StringComparison.Ordinal))
			{
				commandParts[i] = string.Concat("-std=", part.AsSpan("-std:".Length));
			}
			else if (part.StartsWith('/'))
			{
				commandParts.RemoveAt(i);
			}
		}

		commandParts[0] = commandParts[0].Replace("CLANG_~1", "clang");

		commandParts.Insert(1, "-c");

		if (!commandParts.Contains("-emit-llvm"))
		{
			commandParts.Add("-emit-llvm");
		}
		if (!commandParts.Contains("-w"))
		{
			commandParts.Add("-w"); // Suppress warnings
		}
	}

	[SupportedOSPlatform("windows")]
	static bool RunCommand(IEnumerable<string> commandParts, string workingDir)
	{
		ProcessStartInfo psi = new("cmd.exe", commandParts.Prepend("/C"))
		{
			WorkingDirectory = workingDir,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		using Process proc = new() { StartInfo = psi, EnableRaisingEvents = true };

		proc.OutputDataReceived += (sender, e) =>
		{
			if (e.Data is not null)
			{
				Console.WriteLine(e.Data);
			}
		};

		proc.ErrorDataReceived += (sender, e) =>
		{
			if (e.Data is not null)
			{
				Console.Error.WriteLine(e.Data);
			}
		};

		proc.Start();
		proc.BeginOutputReadLine();
		proc.BeginErrorReadLine();
		proc.WaitForExit();

		return proc.ExitCode == 0;
	}

	static bool HasMain(string? mainFunctionDetector, string path)
	{
		if (string.IsNullOrEmpty(mainFunctionDetector))
		{
			return false; // Main function detection is not enabled.
		}

		ProcessStartInfo psi = new(mainFunctionDetector, [path])
		{
			UseShellExecute = false,
		};

		using Process proc = new() { StartInfo = psi };

		proc.Start();
		proc.WaitForExit();

		return proc.ExitCode != 0;
	}

	[SupportedOSPlatform("windows")]
	static bool LlvmLinkExists()
	{
		ProcessStartInfo psi = new("cmd.exe", "/C llvm-link --version")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		Process proc = new() { StartInfo = psi, EnableRaisingEvents = true };
		proc.Start();
		proc.BeginOutputReadLine();
		proc.BeginErrorReadLine();
		proc.WaitForExit();
		return proc.ExitCode == 0;
	}

	static void CreateResponseFile(IEnumerable<string> args, string outputPath)
	{
		IEnumerable<string> lines = args.Select(static arg =>
		{
			if (!arg.Contains(' ') && !arg.Contains('"'))
			{
				return arg;
			}
			else
			{
				string escaped = arg.Replace("\\", "\\\\").Replace("\"", "\\\"");
				return $"\"{escaped}\"";
			}
		});

		File.WriteAllLines(outputPath, lines);
	}
}
