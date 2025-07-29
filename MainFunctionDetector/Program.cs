using LLVMSharp.Interop;
using System.Runtime.InteropServices;

namespace MainFunctionDetector;

internal static unsafe class Program
{
	static void Main(string[] args)
	{
		string path = args[0];
		bool containsMain = ContainsMainFunction(Path.GetFileName(path), File.ReadAllBytes(path));
		Environment.ExitCode = containsMain ? 1 : 0;
	}

	public static bool ContainsMainFunction(string name, ReadOnlySpan<byte> content)
	{
		fixed (byte* ptr = content)
		{
			nint namePtr = Marshal.StringToHGlobalAnsi(name);
			LLVMMemoryBufferRef buffer = LLVM.CreateMemoryBufferWithMemoryRange((sbyte*)ptr, (nuint)content.Length, (sbyte*)namePtr, 1);
			try
			{
				LLVMContextRef context = LLVMContextRef.Create();
				try
				{
					LLVMModuleRef module = context.ParseIR(buffer);
					return module.GetNamedFunction("main") != default;
				}
				finally
				{
					// https://github.com/dotnet/LLVMSharp/issues/234
					//context.Dispose();
				}
			}
			finally
			{
				// This fails randomly with no real explanation.
				// I'm fairly certain that the IR text data is only referenced (not copied),
				// so the memory leak of not disposing the buffer is probably not a big deal.
				// https://github.com/dotnet/LLVMSharp/issues/234
				//LLVM.DisposeMemoryBuffer(buffer);

				Marshal.FreeHGlobal(namePtr);
			}
		}
	}
}
