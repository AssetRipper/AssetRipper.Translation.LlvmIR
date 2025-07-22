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
			if (directory is not null)
			{
				Directory.CreateDirectory(directory);
			}
		}

		string json = File.ReadAllText(compileDbPath);
		List<CompileCommand> entries = JsonSerializer.Deserialize<List<CompileCommand>>(json)!;

		List<string> bcFiles = new(entries.Count);

		try
		{
			foreach (CompileCommand entry in entries)
			{
				List<string> commandParts = CommandParser.ParseCommand(entry.Command);

				string bcOutputPath = Path.ChangeExtension(entry.File, ".bc");
				Directory.CreateDirectory(Path.GetDirectoryName(bcOutputPath)!);
				RewriteCommand(commandParts, bcOutputPath);

				Console.WriteLine($">> Compiling {Path.GetFileName(entry.File)} -> {bcOutputPath}");
				bool success = RunCommand(commandParts, entry.Directory);
				if (!success)
				{
					Console.Error.WriteLine($"Failed to compile {entry.File}");
					return;
				}
				bcFiles.Add(bcOutputPath);
			}

			Console.WriteLine($">> Linking all .bc files into {outputPath}");

			string tempFile = "temp_args.txt";
			CreateResponseFile(bcFiles.Where(arguments.ShouldInclude), tempFile);
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

	static void RewriteCommand(List<string> commandParts, string outputPath)
	{
		if (commandParts.Count is 0)
		{
			throw new ArgumentException("There must be at least one command part", nameof(commandParts));
		}

		for (int i = commandParts.Count - 1; i >= 0; i--)
		{
			string part = commandParts[i];
			if (part is "/nologo" or "-TP" or "/DWIN32" or "/D_WINDOWS" or "/W3" or "/GR" or "/EHsc" or "--" or "-MDd")
			{
				// Remove bad parts
				commandParts.RemoveAt(i);
			}
			else if (part is "-c")
			{
				// Only one -c is allowed, and we add it later.
				commandParts.RemoveAt(i);
			}
			else if (part.StartsWith("-std:", StringComparison.Ordinal))
			{
				commandParts.RemoveAt(i);
			}
			else if (part.StartsWith("/W", StringComparison.Ordinal))
			{
				commandParts.RemoveAt(i);
			}
			else if (part.StartsWith("/w", StringComparison.Ordinal))
			{
				commandParts.RemoveAt(i);
			}
			else if (part.StartsWith("/Fd", StringComparison.Ordinal))
			{
				commandParts.RemoveAt(i);
			}
			else if (part.StartsWith("/Fo", StringComparison.Ordinal))
			{
				commandParts[i] = "-o";
				commandParts.Insert(i + 1, outputPath);
			}
		}

		commandParts[0] = commandParts[0].Replace("clang-cl", "clang");

		commandParts.Insert(1, "-c");
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
