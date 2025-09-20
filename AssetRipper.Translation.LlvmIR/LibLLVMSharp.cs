using LLVMSharp.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace AssetRipper.Translation.LlvmIR;

internal static unsafe partial class LibLLVMSharp
{
	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Free")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial void Free(void* obj);

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Function_getReturnType")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueType* FunctionGetReturnType(LLVMOpaqueValue* Fn);

	public static LLVMTypeRef FunctionGetReturnType(LLVMValueRef fn)
	{
		return FunctionGetReturnType((LLVMOpaqueValue*)fn);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Function_getFunctionType")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueType* FunctionGetFunctionType(LLVMOpaqueValue* Fn);

	public static LLVMTypeRef FunctionGetFunctionType(LLVMValueRef fn)
	{
		return FunctionGetFunctionType((LLVMOpaqueValue*)fn);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_GlobalVariable_getGlobalVariableExpression")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueMetadata* GlobalVariableGetGlobalVariableExpression(LLVMOpaqueValue* global_variable);

	public static LLVMMetadataRef GlobalVariableGetGlobalVariableExpression(LLVMValueRef global_variable)
	{
		return GlobalVariableGetGlobalVariableExpression((LLVMOpaqueValue*)global_variable);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_ConstantDataArray_getData")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial byte* ConstantDataArrayGetData(LLVMOpaqueValue* ConstantDataArray, int* out_size);

	public static ReadOnlySpan<byte> ConstantDataArrayGetData(LLVMValueRef constantDataArray)
	{
		int size;
		byte* data = ConstantDataArrayGetData((LLVMOpaqueValue*)constantDataArray, &size);
		return new ReadOnlySpan<byte>(data, size);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Value_getDemangledName")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial int ValueGetDemangledName(LLVMOpaqueValue* value, byte* buffer, int buffer_size);

	private static int ValueGetDemangledName(LLVMOpaqueValue* value, Span<byte> buffer)
	{
		fixed (byte* bufferPtr = buffer)
		{
			return ValueGetDemangledName(value, bufferPtr, buffer.Length);
		}
	}

	public static string ValueGetDemangledName(LLVMValueRef value)
	{
		const int MaxLength = 4096;
		Span<byte> buffer = stackalloc byte[MaxLength];
		int length = ValueGetDemangledName((LLVMOpaqueValue*)value, buffer);

		if (length > MaxLength)
		{
			buffer = new byte[length];
			length = ValueGetDemangledName((LLVMOpaqueValue*)value, buffer);
		}
		return Encoding.UTF8.GetString(buffer[..length]);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Instruction_hasNoSignedWrap")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	[return: MarshalAs(UnmanagedType.U1)]
	private static partial bool InstructionHasNoSignedWrap(LLVMOpaqueValue* instruction);

	public static bool InstructionHasNoSignedWrap(LLVMValueRef instruction)
	{
		return InstructionHasNoSignedWrap((LLVMOpaqueValue*)instruction);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_Instruction_hasNoUnsignedWrap")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	[return: MarshalAs(UnmanagedType.U1)]
	private static partial bool InstructionHasNoUnsignedWrap(LLVMOpaqueValue* instruction);

	public static bool InstructionHasNoUnsignedWrap(LLVMValueRef instruction)
	{
		return InstructionHasNoUnsignedWrap((LLVMOpaqueValue*)instruction);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DICompositeType_getBaseType")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueMetadata* DICompositeTypeGetBaseType(LLVMOpaqueMetadata* type);

	public static LLVMMetadataRef DICompositeTypeGetBaseType(LLVMMetadataRef type)
	{
		return DICompositeTypeGetBaseType((LLVMOpaqueMetadata*)type);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DICompositeType_getElements")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial void DICompositeTypeGetElements(LLVMOpaqueMetadata* type, LLVMOpaqueMetadata*** out_buffer, int* out_size);

	public static LLVMMetadataRef[] DICompositeTypeGetElements(LLVMMetadataRef type)
	{
		LLVMOpaqueMetadata** buffer = null;
		int size = 0;
		DICompositeTypeGetElements((LLVMOpaqueMetadata*)type, &buffer, &size);
		if (buffer == null)
		{
			if (buffer != null)
			{
				Free(buffer);
			}
			return [];
		}

		if (size == 0)
		{
			Free(buffer);
			return [];
		}

		LLVMMetadataRef[] result = new LLVMMetadataRef[size];
		for (int i = 0; i < size; i++)
		{
			result[i] = (LLVMMetadataRef)buffer[i];
		}

		Free(buffer);

		return result;
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DICompositeType_getIdentifier")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial byte* DICompositeTypeGetIdentifier(LLVMOpaqueMetadata* type, int* out_size);

	public static string? DICompositeTypeGetIdentifier(LLVMMetadataRef type)
	{
		int size = 0;
		byte* ptr = DICompositeTypeGetIdentifier((LLVMOpaqueMetadata*)type, &size);
		if (ptr == null || size == 0)
		{
			return null;
		}
		return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, size));
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DIDerivedType_getBaseType")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueMetadata* DIDerivedTypeGetBaseType(LLVMOpaqueMetadata* type);

	public static LLVMMetadataRef DIDerivedTypeGetBaseType(LLVMMetadataRef type)
	{
		return DIDerivedTypeGetBaseType((LLVMOpaqueMetadata*)type);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DIDerivedType_getEncoding")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial uint DIDerivedTypeGetEncoding(LLVMOpaqueMetadata* type);

	public static uint DIDerivedTypeGetEncoding(LLVMMetadataRef type)
	{
		return DIDerivedTypeGetEncoding((LLVMOpaqueMetadata*)type);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DINode_getTagString")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial sbyte* DINodeGetTagString(LLVMOpaqueMetadata* node, nuint* out_size);

	public static string? DINodeGetTagString(LLVMMetadataRef node)
	{
		nuint size = 0;
		sbyte* ptr = DINodeGetTagString((LLVMOpaqueMetadata*)node, &size);
		if (ptr == null || size == 0)
		{
			return null;
		}
		return Encoding.UTF8.GetString((byte*)ptr, (int)size);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DISubprogram_getType")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueMetadata* DISubprogramGetType(LLVMOpaqueMetadata* subprogram);

	public static LLVMMetadataRef DISubprogramGetType(LLVMMetadataRef subprogram)
	{
		return DISubprogramGetType((LLVMOpaqueMetadata*)subprogram);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DISubprogram_getFlags")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial uint DISubprogramGetFlags(LLVMOpaqueMetadata* subprogram);

	public static LLVMDIFlags DISubprogramGetFlags(LLVMMetadataRef subprogram)
	{
		return (LLVMDIFlags)DISubprogramGetFlags((LLVMOpaqueMetadata*)subprogram);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DISubprogram_getName")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial byte* DISubprogramGetName(LLVMOpaqueMetadata* subprogram, int* out_size);

	public static string? DISubprogramGetName(LLVMMetadataRef subprogram)
	{
		int size = 0;
		byte* ptr = DISubprogramGetName((LLVMOpaqueMetadata*)subprogram, &size);
		if (ptr == null || size == 0)
		{
			return null;
		}
		return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, size));
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DISubprogram_getSPFlags")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial uint DISubprogramGetSPFlags(LLVMOpaqueMetadata* subprogram);

	public static uint DISubprogramGetSPFlags(LLVMMetadataRef subprogram)
	{
		return DISubprogramGetSPFlags((LLVMOpaqueMetadata*)subprogram);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DISubrange_getCount")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueValue* DISubrangeGetCount(LLVMOpaqueMetadata* subrange);

	public static LLVMValueRef DISubrangeGetCount(LLVMMetadataRef subrange)
	{
		return DISubrangeGetCount((LLVMOpaqueMetadata*)subrange);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DISubroutineType_getTypeArray")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial void DISubroutineTypeGetTypeArray(LLVMOpaqueMetadata* subroutine_type, LLVMOpaqueMetadata*** out_buffer, int* out_size);

	public static LLVMMetadataRef[] DISubroutineTypeGetTypeArray(LLVMMetadataRef subroutineType)
	{
		LLVMOpaqueMetadata** buffer = null;
		int size = 0;
		DISubroutineTypeGetTypeArray((LLVMOpaqueMetadata*)subroutineType, &buffer, &size);
		if (buffer == null)
		{
			if (buffer != null)
			{
				Free(buffer);
			}
			return [];
		}

		if (size == 0)
		{
			Free(buffer);
			return [];
		}

		LLVMMetadataRef[] result = new LLVMMetadataRef[size];
		for (int i = 0; i < size; i++)
		{
			result[i] = (LLVMMetadataRef)buffer[i];
		}

		Free(buffer);

		return result;
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DITemplateParameter_getType")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueMetadata* DITemplateParameterGetType(LLVMOpaqueMetadata* parameter);

	public static LLVMMetadataRef DITemplateParameterGetType(LLVMMetadataRef parameter)
	{
		return DITemplateParameterGetType((LLVMOpaqueMetadata*)parameter);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DITemplateValueParameter_getValue")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueMetadata* DITemplateValueParameterGetValue(LLVMOpaqueMetadata* parameter);

	public static LLVMMetadataRef DITemplateValueParameterGetValue(LLVMMetadataRef parameter)
	{
		return DITemplateValueParameterGetValue((LLVMOpaqueMetadata*)parameter);
	}

	public static string? DITypeGetName(LLVMMetadataRef type)
	{
		nuint nameLength = 0;
		sbyte* namePtr = LLVM.DITypeGetName(type, &nameLength);
		if (namePtr == null || nameLength == 0)
		{
			return null;
		}

		return Encoding.UTF8.GetString((byte*)namePtr, (int)nameLength);
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DIVariable_getName")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial byte* DIVariableGetName(LLVMOpaqueMetadata* variable, int* out_size);

	public static string? DIVariableGetName(LLVMMetadataRef variable)
	{
		int size = 0;
		byte* ptr = DIVariableGetName((LLVMOpaqueMetadata*)variable, &size);
		if (ptr == null || size == 0)
		{
			return null;
		}
		return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, size));
	}

	[LibraryImport("libLLVMSharp", EntryPoint = "llvmsharp_DIVariable_getType")]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	private static partial LLVMOpaqueMetadata* DIVariableGetType(LLVMOpaqueMetadata* variable);

	public static LLVMMetadataRef DIVariableGetType(LLVMMetadataRef variable)
	{
		return DIVariableGetType((LLVMOpaqueMetadata*)variable);
	}
}
