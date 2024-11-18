using LLVMSharp.Interop;
using System.Runtime.InteropServices;
using System.Text;

namespace AssetRipper.Translation.Cpp;

internal static unsafe class LibLLVMSharp
{
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
	[DllImport("libLLVMSharp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "llvmsharp_Function_getReturnType", ExactSpelling = true)]
	private static extern LLVMOpaqueType* FunctionGetReturnType(LLVMOpaqueValue* Fn);

	[DllImport("libLLVMSharp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "llvmsharp_Function_getFunctionType", ExactSpelling = true)]
	private static extern LLVMOpaqueType* FunctionGetFunctionType(LLVMOpaqueValue* Fn);

	[DllImport("libLLVMSharp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "llvmsharp_Value_getDemangledName", ExactSpelling = true)]
	private static extern int ValueGetDemangledName(LLVMOpaqueValue* value, byte* buffer, int buffer_size);
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time

	private static int ValueGetDemangledName(LLVMOpaqueValue* value, Span<byte> buffer)
	{
		fixed (byte* bufferPtr = buffer)
		{
			return ValueGetDemangledName(value, bufferPtr, buffer.Length);
		}
	}

	public static LLVMTypeRef FunctionGetReturnType(LLVMValueRef fn)
	{
		return FunctionGetReturnType((LLVMOpaqueValue*)fn);
	}

	public static LLVMTypeRef FunctionGetFunctionType(LLVMValueRef fn)
	{
		return FunctionGetFunctionType((LLVMOpaqueValue*)fn);
	}

	public static string? ValueGetDemangledName(LLVMValueRef value)
	{
		const int MaxLength = 4096;
		Span<byte> buffer = stackalloc byte[MaxLength];
		int length = ValueGetDemangledName((LLVMOpaqueValue*)value, buffer);
		if (length == 0)
		{
			return null;
		}
		return Encoding.UTF8.GetString(buffer[..length]);
	}
}
