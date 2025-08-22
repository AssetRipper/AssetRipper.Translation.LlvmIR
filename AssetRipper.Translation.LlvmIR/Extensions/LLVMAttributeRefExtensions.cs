using LLVMSharp.Interop;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal static class LLVMAttributeRefExtensions
{
	public static unsafe string GetStringAttributeKind(this LLVMAttributeRef attribute)
	{
		uint length = 0;
		sbyte* result = LLVM.GetStringAttributeKind(attribute, &length);
		return Marshal.PtrToStringAnsi((IntPtr)result, (int)length);
	}

	public static unsafe string GetStringAttributeValue(this LLVMAttributeRef attribute)
	{
		uint length = 0;
		sbyte* result = LLVM.GetStringAttributeValue(attribute, &length);
		return Marshal.PtrToStringAnsi((IntPtr)result, (int)length);
	}

	public static unsafe LLVMTypeRef GetTypeAttributeValue(this LLVMAttributeRef attribute)
	{
		return LLVM.GetTypeAttributeValue(attribute);
	}

	public static unsafe ulong GetEnumAttributeValue(this LLVMAttributeRef attribute)
	{
		return LLVM.GetEnumAttributeValue(attribute);
	}

	public static unsafe uint GetEnumAttributeKind(this LLVMAttributeRef attribute)
	{
		return LLVM.GetEnumAttributeKind(attribute);
	}

	public static unsafe bool IsTypeAttribute(this LLVMAttributeRef attribute)
	{
		return LLVM.IsTypeAttribute(attribute) != 0;
	}

	public static unsafe bool IsStringAttribute(this LLVMAttributeRef attribute)
	{
		return LLVM.IsStringAttribute(attribute) != 0;
	}

	public static unsafe bool IsEnumAttribute(this LLVMAttributeRef attribute)
	{
		return LLVM.IsEnumAttribute(attribute) != 0;
	}
}
