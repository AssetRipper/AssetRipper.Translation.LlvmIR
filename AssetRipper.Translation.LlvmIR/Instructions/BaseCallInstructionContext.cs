using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

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
			Debug.Assert(functionCalled.IsVoidReturn && functionCalled.NormalParameters.Length is 1);

			Module.LoadValue(instructions, ArgumentOperands[0]);
			instructions.Add(CilOpCodes.Ldarg, Function!.Definition.Parameters[^1]);// args
			instructions.Add(CilOpCodes.Call, Module.InstructionHelperType.Methods.First(m => m.Name == nameof(InstructionHelper.VAStart)));
		}
		else
		{
			ReadOnlySpan<LLVMValueRef> arguments = ArgumentOperands;

			int variadicParameterCount = arguments.Length - functionCalled.NormalParameters.Length;
			if (!functionCalled.IsVariadic)
			{
				for (int i = 0; i < functionCalled.NormalParameters.Length; i++)
				{
					Module.LoadValue(instructions, arguments[i]);
				}
			}
			else if (variadicParameterCount == 0)
			{
				for (int i = 0; i < functionCalled.NormalParameters.Length; i++)
				{
					Module.LoadValue(instructions, arguments[i]);
				}
				instructions.AddDefaultValue(functionCalled.Definition.Signature!.ParameterTypes[^1]);
			}
			else
			{
				CilLocalVariable intPtrReadOnlySpanLocal = LoadVariadicArguments(instructions, Module, arguments[functionCalled.NormalParameters.Length..]);

				// Push the arguments
				for (int i = 0; i < functionCalled.NormalParameters.Length; i++)
				{
					Module.LoadValue(instructions, arguments[i]);
				}
				instructions.Add(CilOpCodes.Ldloc, intPtrReadOnlySpanLocal);
			}

			instructions.Add(CilOpCodes.Call, functionCalled.Definition);
		}

		if (HasResult)
		{
			AddStore(instructions);
		}
	}

	private static bool IsInvisibleFunction(FunctionContext functionCalled)
	{
		return functionCalled.MangledName is "llvm.va_end";
	}

	/// <summary>
	/// Load variadic arguments into a local variable that contains a read only span of pointers to the arguments.
	/// </summary>
	/// <param name="instructions"></param>
	/// <param name="module"></param>
	/// <param name="variadicArguments"></param>
	/// <returns>A local variable containing a read only span of pointers to the arguments.</returns>
	internal static CilLocalVariable LoadVariadicArguments(CilInstructionCollection instructions, ModuleContext module, ReadOnlySpan<LLVMValueRef> variadicArguments)
	{
		CorLibTypeSignature intPtr = module.Definition.CorLibTypeFactory.IntPtr;

		TypeDefinition intPtrBuffer = module.GetOrCreateInlineArray(intPtr, variadicArguments.Length).Type;

		TypeSignature intPtrSpan = module.Definition.DefaultImporter
			.ImportType(typeof(Span<>))
			.MakeGenericInstanceType(intPtr);

		TypeSignature intPtrReadOnlySpan = module.Definition.DefaultImporter
			.ImportType(typeof(ReadOnlySpan<>))
			.MakeGenericInstanceType(intPtr);

		IMethodDescriptor getTypeFromHandle = module.Definition.DefaultImporter.ImportMethod(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);

		MethodSignature getItemSignature = MethodSignature.CreateInstance(new GenericParameterSignature(GenericParameterType.Type, 0).MakeByReferenceType(), module.Definition.CorLibTypeFactory.Int32);

		IMethodDescriptor intPtrSpanGetItem = new MemberReference(intPtrSpan.ToTypeDefOrRef(), "get_Item", getItemSignature);

		MethodDefinition inlineArrayAsSpan = module.InlineArrayHelperType.Methods.Single(m => m.Name == nameof(InlineArrayHelper.AsSpan));

		MethodDefinition spanToReadOnly = module.SpanHelperType.Methods.Single(m => m.Name == nameof(SpanHelper.ToReadOnly));

		CilLocalVariable intPtrBufferLocal = instructions.AddLocalVariable(intPtrBuffer.ToTypeSignature());
		instructions.AddDefaultValue(intPtrBuffer.ToTypeSignature());
		instructions.Add(CilOpCodes.Stloc, intPtrBufferLocal);

		CilLocalVariable[] variadicLocals = new CilLocalVariable[variadicArguments.Length];
		for (int i = 0; i < variadicLocals.Length; i++)
		{
			module.LoadValue(instructions, variadicArguments[i], out TypeSignature typeSignature);
			CilLocalVariable local = instructions.AddLocalVariable(typeSignature);
			instructions.Add(CilOpCodes.Stloc, local);
			variadicLocals[i] = local;
		}

		CilLocalVariable intPtrSpanLocal = instructions.AddLocalVariable(intPtrSpan);
		instructions.Add(CilOpCodes.Ldloca, intPtrBufferLocal);
		instructions.Add(CilOpCodes.Call, inlineArrayAsSpan.MakeGenericInstanceMethod(intPtrBuffer.ToTypeSignature(), intPtr));
		instructions.Add(CilOpCodes.Stloc, intPtrSpanLocal);

		for (int i = 0; i < variadicArguments.Length; i++)
		{
			instructions.Add(CilOpCodes.Ldloca, intPtrSpanLocal);
			instructions.Add(CilOpCodes.Ldc_I4, i);
			instructions.Add(CilOpCodes.Call, intPtrSpanGetItem);
			instructions.Add(CilOpCodes.Ldloca, variadicLocals[i]);
			instructions.Add(CilOpCodes.Stind_I);
		}

		CilLocalVariable intPtrReadOnlySpanLocal = instructions.AddLocalVariable(intPtrSpan);
		instructions.Add(CilOpCodes.Ldloc, intPtrSpanLocal);
		instructions.Add(CilOpCodes.Call, spanToReadOnly.MakeGenericInstanceMethod(intPtr));
		instructions.Add(CilOpCodes.Stloc, intPtrReadOnlySpanLocal);

		return intPtrReadOnlySpanLocal;
	}
}
