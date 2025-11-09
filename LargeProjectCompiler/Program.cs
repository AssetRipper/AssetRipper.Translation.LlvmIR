using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;

namespace LargeProjectCompiler;

internal static class Program
{
	const string LatestCppStandard = "c++26";

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

		HashSet<string> sourceFiles = new(entries.Count);

		List<string> bcFiles = new(entries.Count);

		string environmentDirectory = Environment.CurrentDirectory;
		try
		{
			for (int i = 0; i < entries.Count; i++)
			{
				CompileCommand entry = entries[i];

				if (!sourceFiles.Add(entry.File))
				{
					continue; // Skip duplicates
				}

				List<string> commandParts = CommandParser.ParseCommand(entry.Command);

				if (commandParts.Count is 0)
				{
					continue; // Skip if empty
				}

				if (!commandParts[0].Contains("CLANG_~1", StringComparison.Ordinal))
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
				RewriteCommand(commandParts, bcOutputPath);

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
				bcFiles.Add(Path.GetFullPath(bcOutputPath));
				if (HasMain(bcOutputPath))
				{
					Console.Error.WriteLine($"Main function detected in {bcOutputPath}");
					return;
				}
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
			Console.WriteLine($">> Removing {bcFiles.Count} BC files");
			foreach (string bcFile in bcFiles)
			{
				if (File.Exists(bcFile))
				{
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

		bool foundOutput = false;
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
				foundOutput = true;
			}
			else if (part is "-o" && i < commandParts.Count - 1)
			{
				commandParts[i + 1] = outputPath;
				foundOutput = true;
			}
			else if (part.StartsWith("-std:", StringComparison.Ordinal) || part.StartsWith("/std:", StringComparison.Ordinal))
			{
				ReadOnlySpan<char> value = part.AsSpan("-std:".Length);
				if (value.SequenceEqual("c++latest"))
				{
					value = LatestCppStandard;
				}
				commandParts[i] = string.Concat("-std=", value);
			}
			else if (part.StartsWith('/'))
			{
				commandParts.RemoveAt(i);
			}
		}

		if (!foundOutput)
		{
			throw new ArgumentException("Output path not found in command", nameof(commandParts));
		}

		commandParts[0] = commandParts[0].Replace("CLANG_~1", "clang");

		commandParts.Insert(1, "-c");

		AddIfNotPresent(commandParts, "-emit-llvm");
		AddIfNotPresent(commandParts, "-w"); // Suppress warnings
		AddIfNotPresent(commandParts, "-DNOMINMAX"); // Prevent macro conflicts
		AddIfNotPresent(commandParts, "-g"); // Generate debug info
		AddIfNotPresent(commandParts, "-fno-discard-value-names"); // Keep parameter names
		AddIfNotPresent(commandParts, "-fstandalone-debug"); // Ensures that the debug information is self-contained
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

	static unsafe bool HasMain(string path)
	{
		byte[] content = File.ReadAllBytes(path);
		fixed (byte* ptr = content)
		{
			using LLVMContextRef context = LLVMContextRef.Create();
			nint namePtr = Marshal.StringToHGlobalAnsi(Path.GetFileName(path));
			LLVMMemoryBufferRef buffer = LLVM.CreateMemoryBufferWithMemoryRange((sbyte*)ptr, (nuint)content.Length, (sbyte*)namePtr, 0);
			try
			{
				using LLVMModuleRef module = context.ParseIR(buffer);
				return module.GetNamedFunction("main") != default;
			}
			finally
			{
				// This fails randomly with no real explanation.
				// The IR text data is only referenced (not copied),
				// so the memory leak of not disposing the buffer is negligible.
				//LLVM.DisposeMemoryBuffer(buffer);

				Marshal.FreeHGlobal(namePtr);

				// Collect any memory that got allocated.
				GC.Collect();
			}
		}
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

	static void AddIfNotPresent(List<string> list, string item)
	{
		if (!list.Contains(item))
		{
			list.Add(item);
		}
	}
}
