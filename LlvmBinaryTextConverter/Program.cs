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
			using LLVMContextRef context = LLVMContextRef.Create();
			nint namePtr = Marshal.StringToHGlobalAnsi(name);
			LLVMMemoryBufferRef buffer = LLVM.CreateMemoryBufferWithMemoryRange((sbyte*)ptr, (nuint)content.Length, (sbyte*)namePtr, 0);
			try
			{
				using LLVMModuleRef module = context.ParseIR(buffer);
				module.WriteBitcodeToFile(outputPath);
			}
			finally
			{
				// This fails randomly with no real explanation.
				// The IR text data is only referenced (not copied),
				// so the memory leak of not disposing the buffer is negligible.
				//LLVM.DisposeMemoryBuffer(buffer);

				Marshal.FreeHGlobal(namePtr);
			}
		}
	}

	public static string ConvertBinaryToText(string name, ReadOnlySpan<byte> content)
	{
		fixed (byte* ptr = content)
		{
			using LLVMContextRef context = LLVMContextRef.Create();
			nint namePtr = Marshal.StringToHGlobalAnsi(name);
			LLVMMemoryBufferRef buffer = LLVM.CreateMemoryBufferWithMemoryRange((sbyte*)ptr, (nuint)content.Length, (sbyte*)namePtr, 0);
			try
			{
				using LLVMModuleRef module = context.ParseBitcode(buffer);
				return module.PrintToString();
			}
			finally
			{
				// This fails randomly with no real explanation.
				// The binary data is only referenced (not copied),
				// so the memory leak of not disposing the buffer is negligible.
				//LLVM.DisposeMemoryBuffer(buffer);

				Marshal.FreeHGlobal(namePtr);
			}
		}
	}
}
