using LLVMSharp.Interop;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.Cpp;

internal static unsafe class LibLLVMSharp
{
	[DllImport("libLLVMSharp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "llvmsharp_Function_getReturnType", ExactSpelling = true)]
	private static extern LLVMOpaqueType* FunctionGetReturnType(LLVMOpaqueValue* Fn);

	[DllImport("libLLVMSharp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "llvmsharp_Function_getFunctionType", ExactSpelling = true)]
	private static extern LLVMOpaqueType* FunctionGetFunctionType(LLVMOpaqueValue* Fn);

	[DllImport("libLLVMSharp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "llvmsharp_Value_getDemangledName", ExactSpelling = true)]
	private static extern char* ValueGetDemangledName(LLVMOpaqueValue* value);

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
		return Marshal.PtrToStringAnsi((IntPtr)ValueGetDemangledName((LLVMOpaqueValue*)value));
	}
}
