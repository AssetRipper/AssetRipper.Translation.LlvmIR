using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CallInstructionContext : InstructionContext
{
	internal CallInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMCall);
		Debug.Assert(Operands.Length >= 1);
	}

	public LLVMValueRef FunctionOperand => Operands[^1];
	public ReadOnlySpan<LLVMValueRef> ArgumentOperands => Operands.AsSpan()[..^1];
	public FunctionContext? FunctionCalled => Module.Methods.TryGetValue(FunctionOperand);

	public StandAloneSignature MakeStandaloneSignature()
	{
		TypeSignature[] parameterTypes = new TypeSignature[ArgumentOperands.Length];
		for (int i = 0; i < ArgumentOperands.Length; i++)
		{
			LLVMValueRef operand = ArgumentOperands[i];
			parameterTypes[i] = GetOperandTypeSignature(operand);
		}
		MethodSignature methodSignature = MethodSignature.CreateStatic(ResultTypeSignature, parameterTypes);
		return new StandAloneSignature(methodSignature);
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		FunctionContext? functionCalled = FunctionCalled;
		if (functionCalled is null)
		{
			ReadOnlySpan<LLVMValueRef> arguments = ArgumentOperands;
			for (int i = 0; i < arguments.Length; i++)
			{
				LoadOperand(instructions, arguments[i]);
			}

			LoadOperand(instructions, FunctionOperand);
			instructions.Add(CilOpCodes.Calli, MakeStandaloneSignature());
		}
		else
		{
			ReadOnlySpan<LLVMValueRef> arguments = ArgumentOperands;

			int variadicParameterCount = arguments.Length - functionCalled.Parameters.Length;
			if (!functionCalled.IsVariadic)
			{
				for (int i = 0; i < functionCalled.Parameters.Length; i++)
				{
					LoadOperand(instructions, arguments[i]);
				}
			}
			else if (variadicParameterCount == 0)
			{
				for (int i = 0; i < functionCalled.Parameters.Length; i++)
				{
					LoadOperand(instructions, arguments[i]);
				}
				instructions.AddDefaultValue(functionCalled.Definition.Signature!.ParameterTypes[^2]);
				instructions.AddDefaultValue(functionCalled.Definition.Signature!.ParameterTypes[^1]);
			}
			else
			{
				CorLibTypeSignature intPtr = Module.Definition.CorLibTypeFactory.IntPtr;
				TypeSignature systemType = Module.Definition.DefaultImporter.ImportType(typeof(Type)).ToTypeSignature();

				TypeDefinition intPtrBuffer = Module.GetOrCreateInlineArray(intPtr, variadicParameterCount);
				TypeDefinition systemTypeBuffer = Module.GetOrCreateInlineArray(systemType, variadicParameterCount);

				TypeSignature intPtrSpan = Module.Definition.DefaultImporter
					.ImportType(typeof(Span<>))
					.MakeGenericInstanceType(intPtr);
				TypeSignature typeSpan = Module.Definition.DefaultImporter
					.ImportType(typeof(Span<>))
					.MakeGenericInstanceType(systemType);

				IMethodDescriptor getTypeFromHandle = Module.Definition.DefaultImporter.ImportMethod(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);

				MethodSignature getItemSignature = MethodSignature.CreateInstance(new GenericParameterSignature(GenericParameterType.Type, 0).MakeByReferenceType(), Module.Definition.CorLibTypeFactory.Int32);

				IMethodDescriptor intPtrSpanGetItem = new MemberReference(intPtrSpan.ToTypeDefOrRef(), "get_Item", getItemSignature);

				IMethodDescriptor typeSpanGetItem = new MemberReference(typeSpan.ToTypeDefOrRef(), "get_Item", getItemSignature);

				MethodDefinition inlineArrayAsSpan = Module.InlineArrayHelperType.Methods.Single(m => m.Name == nameof(InlineArrayHelper.InlineArrayAsSpan));

				MethodDefinition spanToReadOnly = Module.InlineArrayHelperType.Methods.Single(m => m.Name == nameof(InlineArrayHelper.ToReadOnly));

				CilLocalVariable intPtrBufferLocal = instructions.AddLocalVariable(intPtrBuffer.ToTypeSignature());
				instructions.AddDefaultValue(intPtrBuffer.ToTypeSignature());
				instructions.Add(CilOpCodes.Stloc, intPtrBufferLocal);

				CilLocalVariable[] variadicLocals = new CilLocalVariable[variadicParameterCount];
				for (int i = functionCalled.Parameters.Length; i < arguments.Length; i++)
				{
					int index = i - functionCalled.Parameters.Length;

					LoadOperand(instructions, arguments[i], out TypeSignature typeSignature);
					CilLocalVariable local = instructions.AddLocalVariable(typeSignature);
					instructions.Add(CilOpCodes.Stloc, local);
					variadicLocals[index] = local;
				}

				CilLocalVariable intPtrSpanLocal = instructions.AddLocalVariable(intPtrSpan);
				instructions.Add(CilOpCodes.Ldloca, intPtrBufferLocal);
				instructions.Add(CilOpCodes.Ldc_I4, variadicParameterCount);
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

				CilLocalVariable systemTypeBufferLocal = instructions.AddLocalVariable(systemTypeBuffer.ToTypeSignature());
				instructions.AddDefaultValue(systemTypeBuffer.ToTypeSignature());
				instructions.Add(CilOpCodes.Stloc, systemTypeBufferLocal);

				CilLocalVariable typeSpanLocal = instructions.AddLocalVariable(typeSpan);
				instructions.Add(CilOpCodes.Ldloca, systemTypeBufferLocal);
				instructions.Add(CilOpCodes.Ldc_I4, variadicParameterCount);
				instructions.Add(CilOpCodes.Call, inlineArrayAsSpan.MakeGenericInstanceMethod(systemTypeBuffer.ToTypeSignature(), systemType));
				instructions.Add(CilOpCodes.Stloc, typeSpanLocal);

				for (int i = functionCalled.Parameters.Length; i < arguments.Length; i++)
				{
					TypeSignature type = GetOperandTypeSignature(arguments[i]);
					instructions.Add(CilOpCodes.Ldloca, typeSpanLocal);
					instructions.Add(CilOpCodes.Ldc_I4, i - functionCalled.Parameters.Length);
					instructions.Add(CilOpCodes.Call, typeSpanGetItem);
					instructions.Add(CilOpCodes.Ldtoken, type.ToTypeDefOrRef());
					instructions.Add(CilOpCodes.Call, getTypeFromHandle);
					instructions.Add(CilOpCodes.Stind_Ref);
				}

				// Push the arguments
				for (int i = 0; i < functionCalled.Parameters.Length; i++)
				{
					LoadOperand(instructions, arguments[i]);
				}
				instructions.Add(CilOpCodes.Ldloc, intPtrSpanLocal);
				instructions.Add(CilOpCodes.Call, spanToReadOnly.MakeGenericInstanceMethod(intPtr));
				instructions.Add(CilOpCodes.Ldloc, typeSpanLocal);
				instructions.Add(CilOpCodes.Call, spanToReadOnly.MakeGenericInstanceMethod(systemType));
			}

			instructions.Add(CilOpCodes.Call, functionCalled.Definition);
		}

		if (HasResult)
		{
			instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
		}
	}
}
