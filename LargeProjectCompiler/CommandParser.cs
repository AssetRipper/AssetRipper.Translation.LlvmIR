using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace LargeProjectCompiler;

internal static class CommandParser
{
	[SupportedOSPlatform("windows")]
	public static unsafe List<string> ParseCommand(string command)
	{
		ArgumentException.ThrowIfNullOrEmpty(command);

		List<string> arguments = [];
		fixed (char* ptr = command)
		{
			int argc = 0;
			char** argv = CommandLineToArgvW(ptr, &argc);
			if (argv == null)
			{
				throw new System.ComponentModel.Win32Exception();
			}

			try
			{
				for (int i = 0; i < argc; i++)
				{
					string? arg = Marshal.PtrToStringUni((nint)argv[i]);
					if (string.IsNullOrEmpty(arg))
					{
						throw new System.ComponentModel.Win32Exception();
					}
					arguments.Add(arg);
				}
			}
			finally
			{
				if (LocalFree((HLOCAL)argv) != HLOCAL.NULL)
				{
					Console.Error.WriteLine("Failed to free command line arguments.");
				}
			}
		}

		return arguments;
	}
}
