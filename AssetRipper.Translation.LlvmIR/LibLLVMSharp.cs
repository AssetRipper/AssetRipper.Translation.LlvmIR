using LLVMSharp.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace AssetRipper.Translation.LlvmIR;

internal static unsafe partial class LibLLVMSharp
{
	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Function_getReturnType")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueType* FunctionGetReturnType(LLVMOpaqueValue* Fn);

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Function_getFunctionType")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueType* FunctionGetFunctionType(LLVMOpaqueValue* Fn);

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_ConstantDataArray_getData")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial byte* ConstantDataArrayGetData(LLVMOpaqueValue* ConstantDataArray, int* out_size);

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Value_getDemangledName")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial int ValueGetDemangledName(LLVMOpaqueValue* value, byte* buffer, int buffer_size);

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Instruction_hasNoSignedWrap")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	[return: MarshalAs(UnmanagedType.U1)]
	private static partial bool InstructionHasNoSignedWrap(LLVMOpaqueValue* instruction);

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Instruction_hasNoUnsignedWrap")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	[return: MarshalAs(UnmanagedType.U1)]
	private static partial bool InstructionHasNoUnsignedWrap(LLVMOpaqueValue* instruction);

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

	public static ReadOnlySpan<byte> ConstantDataArrayGetData(LLVMValueRef constantDataArray)
	{
		int size;
		byte* data = ConstantDataArrayGetData((LLVMOpaqueValue*)constantDataArray, &size);
		return new ReadOnlySpan<byte>(data, size);
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

	public static bool InstructionHasNoSignedWrap(LLVMValueRef instruction)
	{
		return InstructionHasNoSignedWrap((LLVMOpaqueValue*)instruction);
	}

	public static bool InstructionHasNoUnsignedWrap(LLVMValueRef instruction)
	{
		return InstructionHasNoUnsignedWrap((LLVMOpaqueValue*)instruction);
	}
}
