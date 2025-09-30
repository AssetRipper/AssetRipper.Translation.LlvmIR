using LLVMSharp.Interop;
using System.Runtime.InteropServices;

namespace SizeOptimizer;

internal static unsafe class Program
{
	static void Main(string[] args)
	{
		string input = args[0];
		string output = args[1];
		Run(Path.GetFileName(input), File.ReadAllBytes(input), output);
	}

	public static void Run(string name, ReadOnlySpan<byte> content, string outputPath)
	{
		fixed (byte* ptr = content)
		{
			using LLVMContextRef context = LLVMContextRef.Create();
			nint namePtr = Marshal.StringToHGlobalAnsi(name);
			LLVMMemoryBufferRef buffer = LLVM.CreateMemoryBufferWithMemoryRange((sbyte*)ptr, (nuint)content.Length, (sbyte*)namePtr, 0);
			try
			{
				using LLVMModuleRef module = context.ParseIR(buffer);
				OptimizeModule(module, context);
				module.PrintToFile(outputPath);
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

	private static void OptimizeModule(LLVMModuleRef module, LLVMContextRef context)
	{
		// https://github.com/llvm/llvm-project/blob/a8d0ae3412bdbbf3248192c31f94f6649a217b3a/llvm/include/llvm/IR/Attributes.td
		ReadOnlySpan<uint> attributesToAdd =
		[
			GetEnumAttributeKindForName("optsize"u8),
			GetEnumAttributeKindForName("minsize"u8),
		];
		ReadOnlySpan<uint> attributesToRemove =
		[
			GetEnumAttributeKindForName("optnone"u8),
			GetEnumAttributeKindForName("alwaysinline"u8),
		];

		foreach (LLVMValueRef function in module.GetFunctions())
		{
			function.Linkage = LLVMLinkage.LLVMExternalLinkage;

			foreach (uint attribute in attributesToRemove)
			{
				LLVM.RemoveEnumAttributeAtIndex(function, LLVMAttributeIndex.LLVMAttributeFunctionIndex, attribute);
			}
			foreach (uint attribute in attributesToAdd)
			{
				if (LLVM.GetEnumAttributeAtIndex(function, LLVMAttributeIndex.LLVMAttributeFunctionIndex, attribute) == null)
				{
					function.AddAttributeAtIndex(LLVMAttributeIndex.LLVMAttributeFunctionIndex, LLVM.CreateEnumAttribute(context, attribute, default));
				}
			}

			DisableTailCalls(function, context);

			foreach (LLVMBasicBlockRef basicBlock in function.GetBasicBlocks())
			{
				basicBlock.AsValue().Name = ""; // Clear basic block names to reduce size
			}

			foreach (LLVMValueRef instruction in function.GetInstructions())
			{
				instruction.Name = ""; // Clear instruction names to reduce size
				if (instruction.InstructionOpcode is LLVMOpcode.LLVMCall or LLVMOpcode.LLVMInvoke or LLVMOpcode.LLVMCallBr)
				{
					instruction.TailCallKind = LLVMTailCallKind.LLVMTailCallKindNone;
				}
			}
		}

		foreach (LLVMValueRef global in module.GetGlobals())
		{
			global.Linkage = LLVMLinkage.LLVMExternalLinkage;
		}
	}

	private static void DisableTailCalls(LLVMValueRef function, LLVMContextRef context)
	{
		ReadOnlySpan<sbyte> disableTailCallsString = MemoryMarshal.Cast<byte, sbyte>("disable-tail-calls"u8);
		ReadOnlySpan<sbyte> trueString = MemoryMarshal.Cast<byte, sbyte>("true"u8);

		uint disableTailCallsLength = (uint)disableTailCallsString.Length;
		uint trueLength = (uint)trueString.Length;

		fixed (sbyte* disableTailCallsPtr = disableTailCallsString)
		{
			fixed (sbyte* truePtr = trueString)
			{
				LLVMAttributeRef attribute = LLVM.GetStringAttributeAtIndex(function, LLVMAttributeIndex.LLVMAttributeFunctionIndex, disableTailCallsPtr, disableTailCallsLength);
				bool needToRemoveAttribute;
				if (attribute != null)
				{
					uint valueLength = 0;
					sbyte* valuePtr = LLVM.GetStringAttributeValue(attribute, &valueLength);
					if (valuePtr == null)
					{
						needToRemoveAttribute = true;
					}
					else
					{
						needToRemoveAttribute = !new ReadOnlySpan<sbyte>(valuePtr, (int)valueLength).SequenceEqual(trueString);
					}
				}
				else
				{
					needToRemoveAttribute = false;
				}

				bool needToAddAttribute = attribute == null || needToRemoveAttribute;

				if (needToRemoveAttribute)
				{
					LLVM.RemoveStringAttributeAtIndex(function, LLVMAttributeIndex.LLVMAttributeFunctionIndex, disableTailCallsPtr, disableTailCallsLength);
				}

				if (needToAddAttribute)
				{
					attribute = LLVM.CreateStringAttribute(context, disableTailCallsPtr, disableTailCallsLength, truePtr, trueLength);
					function.AddAttributeAtIndex(LLVMAttributeIndex.LLVMAttributeFunctionIndex, attribute);
				}
			}
		}
	}

	private static uint GetEnumAttributeKindForName(ReadOnlySpan<byte> utf8String)
	{
		fixed (byte* ptr = utf8String)
		{
			nint namePtr = (nint)ptr;
			return LLVM.GetEnumAttributeKindForName((sbyte*)namePtr, (nuint)utf8String.Length);
		}
	}

	private static IEnumerable<LLVMValueRef> GetFunctions(this LLVMModuleRef module)
	{
		LLVMValueRef function = module.FirstFunction;
		while (function.Handle != 0)
		{
			yield return function;
			function = function.NextFunction;
		}
	}

	private static IEnumerable<LLVMValueRef> GetGlobals(this LLVMModuleRef module)
	{
		LLVMValueRef global = module.FirstGlobal;
		while (global.Handle != 0)
		{
			yield return global;
			global = global.NextGlobal;
		}
	}

	private static IEnumerable<LLVMValueRef> GetInstructions(this LLVMBasicBlockRef basicBlock)
	{
		LLVMValueRef instruction = basicBlock.FirstInstruction;
		while (instruction.Handle != 0)
		{
			yield return instruction;
			instruction = instruction.NextInstruction;
		}
	}

	private static IEnumerable<LLVMValueRef> GetInstructions(this LLVMValueRef value)
	{
		if (value.IsAFunction != default)
		{
			return GetFunctionInstructions(value);
		}
		else if (value.IsABasicBlock != default)
		{
			return value.AsBasicBlock().GetInstructions();
		}
		else if (value.IsAInstruction != default)
		{
			return [value];
		}
		else
		{
			return [];
		}

		static IEnumerable<LLVMValueRef> GetFunctionInstructions(LLVMValueRef function)
		{
			foreach (LLVMBasicBlockRef basicBlock in function.GetBasicBlocks())
			{
				foreach (LLVMValueRef instruction in basicBlock.GetInstructions())
				{
					yield return instruction;
				}
			}
		}
	}
}
