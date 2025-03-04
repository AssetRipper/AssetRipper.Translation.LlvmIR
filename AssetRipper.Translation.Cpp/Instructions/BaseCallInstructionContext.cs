using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal abstract class BaseCallInstructionContext : InstructionContext
{
	protected BaseCallInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		ArgumentCount = (int)CalledFunctionTypeRef.ParamTypesCount;
	}

	public LLVMValueRef FunctionOperand => Operands[^1];
	public ReadOnlySpan<LLVMValueRef> ArgumentOperands => Operands.AsSpan(0, ArgumentCount);
	private int ArgumentCount { get; }
	public unsafe LLVMTypeRef CalledFunctionTypeRef => LLVM.GetCalledFunctionType(Instruction);
	public FunctionContext? CalledFunction => Module.Methods.TryGetValue(FunctionOperand);

	public StandAloneSignature MakeStandaloneSignature()
	{
		TypeSignature[] parameterTypes = new TypeSignature[ArgumentOperands.Length];
		for (int i = 0; i < ArgumentOperands.Length; i++)
		{
			LLVMValueRef operand = ArgumentOperands[i];
			parameterTypes[i] = Module.GetTypeSignature(operand);
		}
		MethodSignature methodSignature = MethodSignature.CreateStatic(ResultTypeSignature, parameterTypes);
		return new StandAloneSignature(methodSignature);
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		FunctionContext? functionCalled = CalledFunction;
		if (functionCalled is null)
		{
			ReadOnlySpan<LLVMValueRef> arguments = ArgumentOperands;
			for (int i = 0; i < arguments.Length; i++)
			{
				Module.LoadValue(instructions, arguments[i]);
			}

			Module.LoadValue(instructions, FunctionOperand);
			instructions.Add(CilOpCodes.Calli, MakeStandaloneSignature());
		}
		else if (IsInvisibleFunction(functionCalled))
		{
			Debug.Assert(functionCalled.IsVoidReturn);
		}
		else if (functionCalled.MangledName is "llvm.va_start")
		{
			Debug.Assert(functionCalled.IsVoidReturn && functionCalled.Parameters.Length is 1);

			Module.LoadValue(instructions, ArgumentOperands[0]);
			instructions.Add(CilOpCodes.Ldarg, Function!.Definition.Parameters[^1]);// args
			instructions.Add(CilOpCodes.Call, Module.InstructionHelperType.Methods.First(m => m.Name == nameof(InstructionHelper.VAStart)));
		}
		else
		{
			ReadOnlySpan<LLVMValueRef> arguments = ArgumentOperands;

			int variadicParameterCount = arguments.Length - functionCalled.Parameters.Length;
			if (!functionCalled.IsVariadic)
			{
				for (int i = 0; i < functionCalled.Parameters.Length; i++)
				{
					Module.LoadValue(instructions, arguments[i]);
				}
			}
			else if (variadicParameterCount == 0)
			{
				for (int i = 0; i < functionCalled.Parameters.Length; i++)
				{
					Module.LoadValue(instructions, arguments[i]);
				}
				instructions.AddDefaultValue(functionCalled.Definition.Signature!.ParameterTypes[^2]);
				instructions.AddDefaultValue(functionCalled.Definition.Signature!.ParameterTypes[^1]);
			}
			else
			{
				CorLibTypeSignature intPtr = Module.Definition.CorLibTypeFactory.IntPtr;

				TypeDefinition intPtrBuffer = Module.GetOrCreateInlineArray(intPtr, variadicParameterCount).Type;

				TypeSignature intPtrSpan = Module.Definition.DefaultImporter
					.ImportType(typeof(Span<>))
					.MakeGenericInstanceType(intPtr);

				IMethodDescriptor getTypeFromHandle = Module.Definition.DefaultImporter.ImportMethod(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);

				MethodSignature getItemSignature = MethodSignature.CreateInstance(new GenericParameterSignature(GenericParameterType.Type, 0).MakeByReferenceType(), Module.Definition.CorLibTypeFactory.Int32);

				IMethodDescriptor intPtrSpanGetItem = new MemberReference(intPtrSpan.ToTypeDefOrRef(), "get_Item", getItemSignature);

				MethodDefinition inlineArrayAsSpan = Module.InlineArrayHelperType.Methods.Single(m => m.Name == nameof(InlineArrayHelper.AsSpan));

				MethodDefinition spanToReadOnly = Module.SpanHelperType.Methods.Single(m => m.Name == nameof(SpanHelper.ToReadOnly));

				CilLocalVariable intPtrBufferLocal = instructions.AddLocalVariable(intPtrBuffer.ToTypeSignature());
				instructions.AddDefaultValue(intPtrBuffer.ToTypeSignature());
				instructions.Add(CilOpCodes.Stloc, intPtrBufferLocal);

				CilLocalVariable[] variadicLocals = new CilLocalVariable[variadicParameterCount];
				for (int i = functionCalled.Parameters.Length; i < arguments.Length; i++)
				{
					int index = i - functionCalled.Parameters.Length;

					Module.LoadValue(instructions, arguments[i], out TypeSignature typeSignature);
					CilLocalVariable local = instructions.AddLocalVariable(typeSignature);
					instructions.Add(CilOpCodes.Stloc, local);
					variadicLocals[index] = local;
				}

				CilLocalVariable intPtrSpanLocal = instructions.AddLocalVariable(intPtrSpan);
				instructions.Add(CilOpCodes.Ldloca, intPtrBufferLocal);
				instructions.Add(CilOpCodes.Call, inlineArrayAsSpan.MakeGenericInstanceMethod(intPtrBuffer.ToTypeSignature(), intPtr));
				instructions.Add(CilOpCodes.Stloc, intPtrSpanLocal);

				for (int i = functionCalled.Parameters.Length; i < arguments.Length; i++)
				{
					int index = i - functionCalled.Parameters.Length;

					instructions.Add(CilOpCodes.Ldloca, intPtrSpanLocal);
					instructions.Add(CilOpCodes.Ldc_I4, index);
					instructions.Add(CilOpCodes.Call, intPtrSpanGetItem);
					instructions.Add(CilOpCodes.Ldloca, variadicLocals[index]);
					instructions.Add(CilOpCodes.Stind_I);
				}

				// Push the arguments
				for (int i = 0; i < functionCalled.Parameters.Length; i++)
				{
					Module.LoadValue(instructions, arguments[i]);
				}
				instructions.Add(CilOpCodes.Ldloc, intPtrSpanLocal);
				instructions.Add(CilOpCodes.Call, spanToReadOnly.MakeGenericInstanceMethod(intPtr));
			}

			instructions.Add(CilOpCodes.Call, functionCalled.Definition);
		}

		if (HasResult)
		{
			instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
		}
	}

	private static bool IsInvisibleFunction(FunctionContext functionCalled)
	{
		return functionCalled.MangledName is "llvm.va_end";
	}
}
