using LLVMSharp.Interop;
using System.Runtime.InteropServices;

namespace LlvmBinaryTextConverter;

internal static unsafe class Program
{
	static void Main(string[] args)
	{
		string path = args[0];
		switch(Path.GetExtension(path))
		{
			case ".ll":
			{
				string outputPath = Path.ChangeExtension(path, ".bc");
				string name = Path.GetFileNameWithoutExtension(path);
				byte[] content = File.ReadAllBytes(path);
				ConvertTextToBinary(name, content, outputPath);
				break;
			}
			case ".bc":
			{
				string outputPath = Path.ChangeExtension(path, ".ll");
				string name = Path.GetFileNameWithoutExtension(path);
				byte[] content = File.ReadAllBytes(path);
				string textContent = ConvertBinaryToText(name, content);
				File.WriteAllText(outputPath, textContent);
				break;
			}
			default:
				throw new NotSupportedException($"Unsupported file extension: {Path.GetExtension(path)}");
		}
	}

	public static void ConvertTextToBinary(string name, ReadOnlySpan<byte> content, string outputPath)
	{
		fixed (byte* ptr = content)
		{
			nint namePtr = Marshal.StringToHGlobalAnsi(name);
			nint outputPathPtr = Marshal.StringToHGlobalAnsi(outputPath);
			LLVMMemoryBufferRef buffer = LLVM.CreateMemoryBufferWithMemoryRange((sbyte*)ptr, (nuint)content.Length, (sbyte*)namePtr, 1);
			try
			{
				LLVMContextRef context = LLVMContextRef.Create();
				try
				{
					LLVMModuleRef module = context.ParseIR(buffer);
					LLVM.WriteBitcodeToFile(module, (sbyte*)outputPathPtr);
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
				Marshal.FreeHGlobal(outputPathPtr);
			}
		}
	}

	public static string ConvertBinaryToText(string name, ReadOnlySpan<byte> content)
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
					LLVMModuleRef module = context.ParseBitcode(buffer);
					return module.PrintToString();
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
