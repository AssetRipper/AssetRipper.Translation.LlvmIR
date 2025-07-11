﻿using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetRipper.Translation.LlvmIR;

#pragma warning disable IDE0060 // Remove unused parameter
internal static unsafe partial class IntrinsicFunctions
{
	public static class Implemented
	{
	}
	public static class Unimplemented
	{
	}

	[MangledName("puts")]
	public static int PutString(sbyte* P_0)
	{
		try
		{
			string? s = Marshal.PtrToStringAnsi((IntPtr)P_0); // Maybe UTF-8?
			Console.WriteLine(s);
			return 0;
		}
		catch
		{
			return -1;
		}
	}

	[MangledName("_wassert")]
	[MightThrow]
	public static void Assert(char* message, char* file, uint line)
	{
		ExceptionInfo.Current = new AssertExceptionInfo($"Assertion failed: {Marshal.PtrToStringUni((IntPtr)message)} at {Marshal.PtrToStringUni((IntPtr)file)}:{line}");
	}

	/// <summary>
	/// Triggers a fatal exception, indicating a critical assertion failure in the application.
	/// </summary>
	/// <remarks>
	/// This aligns with the C++ behavior, which causes the application to crash and triggers the Windows Error Reporting (WER) system (aka "Watson").
	/// </remarks>
	/// <param name="expression">A pointer to the string representation of the failed assertion expression.</param>
	/// <param name="function">A pointer to the string representation of the function name where the assertion failed.</param>
	/// <param name="file">A pointer to the string representation of the file name where the assertion failed.</param>
	/// <param name="line">The line number in the file where the assertion failed.</param>
	/// <param name="reserved">Reserved for future use. Currently unused. C++ type is uintptr_t.</param>
	/// <exception cref="FatalException">
	/// Always thrown to indicate a fatal assertion failure. The exception message includes details about the failed
	/// assertion, such as the expression, function, file, and line number.
	/// </exception>
	[DoesNotReturn]
	[MangledName("_invoke_watson")]
	public static void InvokeWatson(char* expression, char* function, char* file, int line, long reserved)
	{
		throw new FatalException($"Fatal assertion failed: {Marshal.PtrToStringUni((IntPtr)expression)} in {Marshal.PtrToStringUni((IntPtr)function)} at {Marshal.PtrToStringUni((IntPtr)file)}:{line}");
	}

	[DoesNotReturn]
	[MangledName("__std_terminate")]
	public static void Terminate()
	{
		throw new FatalException(nameof(Terminate));
	}

	[MangledName("llvm.va.start")]
	public static void llvm_va_start(void** va_list)
	{
		// Handled elsewhere.
		throw new NotSupportedException();
	}

	[MangledName("llvm.va.copy")]
	public static void llvm_va_copy(void** destination, void** source)
	{
		*destination = *source;
	}

	[MangledName("llvm.va.end")]
	public static void llvm_va_end(void** va_list)
	{
		// Do nothing because it's freed automatically.
	}

	[MangledName("strcmp")]
	public static int strcmp(byte* p1, byte* p2)
	{
		while (*p1 == *p2 && *p1 != '\0') // keep going while bytes match
		{
			++p1;
			++p2;
		}
		return *p1 - *p2; // positive, negative, or zero
	}

	[MangledName("memcmp")]
	public static int memcmp(byte* p1, byte* p2, long count)
	{
		for (long i = 0; i < count; i++)
		{
			if (p1[i] != p2[i])
			{
				return p1[i] - p2[i]; // Return the difference of the first non-matching bytes
			}
		}
		return 0; // All bytes match
	}

	[MangledName("llvm.memcpy.p0.p0.i32")]
	public static void llvm_memcpy_p0_p0_i32(void* destination, void* source, int length, bool isVolatile)
	{
		Unsafe.CopyBlock(destination, source, (uint)length);
	}

	[MangledName("llvm.memcpy.p0.p0.i64")]
	public static void llvm_memcpy_p0_p0_i64(void* destination, void* source, long length, bool isVolatile)
	{
		Unsafe.CopyBlock(destination, source, (uint)length);
	}

	[MangledName("llvm.memmove.p0.p0.i32")]
	public static void llvm_memmove_p0_p0_i32(void* destination, void* source, int length, bool isVolatile)
	{
		// Same as memcpy, except that the source and destination are allowed to overlap.
		byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
		Span<byte> span = new(buffer, 0, length);
		new ReadOnlySpan<byte>(source, length).CopyTo(span);
		span.CopyTo(new Span<byte>(destination, length));
		ArrayPool<byte>.Shared.Return(buffer);
	}

	[MangledName("llvm.memmove.p0.p0.i64")]
	public static void llvm_memmove_p0_p0_i64(void* destination, void* source, long length, bool isVolatile)
	{
		llvm_memmove_p0_p0_i32(destination, source, (int)length, isVolatile);
	}

	[MangledName("llvm.memset.p0.i32")]
	public static void llvm_memset_p0_i32(void* destination, sbyte value, int length, bool isVolatile)
	{
		new Span<byte>(destination, length).Fill(unchecked((byte)value));
	}

	[MangledName("llvm.memset.p0.i64")]
	public static void llvm_memset_p0_i64(void* destination, sbyte value, long length, bool isVolatile)
	{
		llvm_memset_p0_i32(destination, value, (int)length, isVolatile);
	}

	[MangledName("calloc")]
	public static void* CAlloc(long elementCount, long elementSize)
	{
		// https://en.cppreference.com/w/c/memory/calloc

		if (elementCount <= 0 || elementSize <= 0)
		{
			return null; // Return null for zero or negative allocation
		}

		if (elementCount > int.MaxValue || elementSize > int.MaxValue)
		{
			return null; // Return null for allocations that exceed int.MaxValue
		}

		long totalSize = elementCount * elementSize;

		if (totalSize > int.MaxValue)
		{
			return null; // Return null for allocations that exceed int.MaxValue
		}

		void* result = Alloc(totalSize);

		// Zero the allocated memory
		new Span<byte>(result, (int)totalSize).Clear();

		return result;
	}

	[MangledName("malloc")]
	[MangledName("??2@YAPEAX_K@Z")] // new
	public static void* Alloc(long size)
	{
		return (void*)Marshal.AllocHGlobal((nint)size);
	}

	[MangledName("realloc")]
	public static void* ReAlloc(void* ptr, long size)
	{
		return (void*)Marshal.ReAllocHGlobal((nint)ptr, (nint)size);
	}

	[MangledName("free")]
	public static void Free(void* ptr)
	{
		Marshal.FreeHGlobal((IntPtr)ptr);
	}

	[MangledName("??3@YAXPEAX_K@Z")]
	public static void Delete(void* ptr, long size)
	{
		Marshal.FreeHGlobal((IntPtr)ptr);
	}

	[MangledName("expand")]
	public static void* Expand(void* ptr, long size)
	{
		// _expand is a non-standard function available in some C++ implementations, particularly in Microsoft C Runtime Library (CRT).
		// It is used to resize a previously allocated memory block without moving it, meaning it tries to expand or shrink the allocated memory in place.
		// _expand is mainly useful for optimizing performance in memory management when using Microsoft CRT.
		// If the block cannot be resized in place, _expand returns NULL, but the original block remains valid.

		// We take advantage of the fact that it's just an optimization and return null, signaling that we can't expand the memory in place.
		return null;
	}

	[MangledName("_CxxThrowException")]
	[MightThrow]
	public static void CxxThrowException(void* exceptionPointer, void* throwInfo)
	{
		ExceptionInfo.Current = new NativeExceptionInfo(exceptionPointer, (ThrowInfo*)throwInfo);
	}

	[MangledName("__CxxFrameHandler3")]
	public static int CxxFrameHandler3(ReadOnlySpan<nint> args)
	{
		if (args.Length != 3)
		{
			throw new ArgumentException("Expected 3 arguments", nameof(args));
		}

		if (args[0] == 0 || args[1] == 0 || args[2] == 0)
		{
			throw new ArgumentNullException(nameof(args), "Arguments cannot be null");
		}

		RttiTypeDescriptor* rttiTypeDescriptor = *(RttiTypeDescriptor**)args[0];
		int unknown = *(int*)args[1];
		void** outException = (void**)args[2];

		if (ExceptionInfo.Current is NativeExceptionInfo nativeException)
		{
			if (rttiTypeDescriptor is not null && !nativeException.Contains(rttiTypeDescriptor))
			{
				return 1; // Continue search
			}

			if (outException != null)
			{
				*outException = nativeException.ExceptionPointer;
			}

			return 0; // Handled
		}
		else
		{
			if (rttiTypeDescriptor != null || outException != null)
			{
				throw new NotSupportedException($"Current exception is not a {nameof(NativeExceptionInfo)}.");
			}
			return 0; // Handled because throwInfo is null
		}
	}

	private sealed class NativeExceptionInfo : ExceptionInfo
	{
		public void* ExceptionPointer { get; private set; }
		public ThrowInfo* ThrowInfo { get; private set; }

		public NativeExceptionInfo(void* exceptionPointer, ThrowInfo* throwInfo)
		{
			ExceptionPointer = exceptionPointer;
			ThrowInfo = throwInfo;
		}

		public bool Contains(RttiTypeDescriptor* rttiTypeDescriptor)
		{
			if (ThrowInfo == null)
			{
				return false;
			}
			foreach (CatchableType catchableType in ThrowInfo->CatchableTypeArray)
			{
				if (catchableType.RttiTypeDescriptor == rttiTypeDescriptor)
				{
					return true;
				}
			}
			return false;
		}

		protected override void Dispose(bool disposing)
		{
			if (ExceptionPointer != null && ThrowInfo != null)
			{
				delegate*<void*, void> destructor = ThrowInfo->Destructor;
				if (destructor != null)
				{
					destructor(ExceptionPointer);
				}
			}
			ExceptionPointer = null;
			ThrowInfo = null;
		}
	}

	private struct ThrowInfo
	{
		public int field_0;
		public int DestructorIndex;
		public int CatchableTypeArrayIndex;

		public readonly delegate*<void*, void> Destructor => (delegate*<void*, void>)PointerIndices.GetPointer(DestructorIndex);
		public readonly ReadOnlySpan<CatchableType> CatchableTypeArray
		{
			get
			{
				CatchableTypeArray* array = (CatchableTypeArray*)PointerIndices.GetPointer(CatchableTypeArrayIndex);
				if (array == null || array->Count <= 0)
				{
					return [];
				}
				return new ReadOnlySpan<CatchableType>((byte*)array + sizeof(int), array->Count);
			}
		}
	}

	private struct CatchableTypeArray
	{
		public int Count;
		// Inline array starts here
	}

	private struct CatchableType
	{
		public int field_0;
		public int RttiTypeDescriptorIndex;
		public int field_2;
		public int field_3;
		public int field_4;
		public int field_5;
		public int ConstructorIndex;

		public readonly RttiTypeDescriptor* RttiTypeDescriptor => (RttiTypeDescriptor*)PointerIndices.GetPointer(RttiTypeDescriptorIndex);

		// Not sure if the signature is always this
		public readonly delegate*<void*, void*, void*> Constructor => (delegate*<void*, void*, void*>)PointerIndices.GetPointer(ConstructorIndex);
	}

	private struct RttiTypeDescriptor
	{
	}

	private sealed class AssertExceptionInfo : ExceptionInfo
	{
		public string Message { get; }
		public AssertExceptionInfo(string message)
		{
			Message = message;
		}
		public override string? GetMessage() => Message;
	}
}
#pragma warning restore IDE0060 // Remove unused parameter
