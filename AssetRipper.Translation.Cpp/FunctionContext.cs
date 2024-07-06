using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp;

internal sealed class FunctionContext
{
	public FunctionContext(LLVMValueRef function, MethodDefinition definition, ModuleContext module)
	{
		Function = function;
		Definition = definition;
		Module = module;
	}

	public LLVMValueRef Function { get; }
	public MethodDefinition Definition { get; }
	public ModuleContext Module { get; }
	public CilInstructionCollection Instructions => Definition.CilMethodBody!.Instructions;
	public Dictionary<LLVMValueRef, CilLocalVariable> InstructionLocals { get; } = new();
	/// <summary>
	/// Some local variables are simply pointers to other local variables, which hold the actual value.
	/// </summary>
	/// <remarks>
	/// Keys are the pointer locals, and the values are the data locals.
	/// </remarks>
	public Dictionary<CilLocalVariable, CilLocalVariable> DataLocals { get; } = new();
	public Dictionary<LLVMBasicBlockRef, CilInstructionLabel> Labels { get; } = new();
	public Dictionary<LLVMValueRef, Parameter> Parameters { get; } = new();

	public void LoadOperand(LLVMValueRef operand)
	{
		LoadOperand(operand, out _);
	}

	public void LoadOperand(LLVMValueRef operand, out TypeSignature typeSignature)
	{
		switch (operand.Kind)
		{
			case LLVMValueKind.LLVMConstantIntValueKind:
				{
					long value = operand.ConstIntSExt;
					LLVMTypeRef operandType = operand.TypeOf;
					if (value is <= int.MaxValue and >= int.MinValue && operandType is { IntWidth: <= sizeof(int) * 8 })
					{
						Instructions.Add(CilOpCodes.Ldc_I4, (int)value);
					}
					else
					{
						Instructions.Add(CilOpCodes.Ldc_I8, value);
					}
					typeSignature = Module.GetTypeSignature(operandType);
				}
				break;
			case LLVMValueKind.LLVMInstructionValueKind:
				{
					CilLocalVariable local = InstructionLocals[operand];
					Instructions.Add(CilOpCodes.Ldloc, local);
					typeSignature = local.VariableType;
				}
				break;
			case LLVMValueKind.LLVMArgumentValueKind:
				{
					Parameter parameter = Parameters[operand];
					Instructions.Add(CilOpCodes.Ldarg, parameter);
					typeSignature = parameter.ParameterType;
				}
				break;
			default:
				throw new NotSupportedException();
		}
	}

	public TypeSignature GetOperandTypeSignature(LLVMValueRef operand)
	{
		return operand.Kind switch
		{
			LLVMValueKind.LLVMConstantIntValueKind => Module.GetTypeSignature(operand.TypeOf),
			LLVMValueKind.LLVMInstructionValueKind => InstructionLocals[operand].VariableType,
			LLVMValueKind.LLVMArgumentValueKind => Parameters[operand].ParameterType,
			_ => throw new NotSupportedException(),
		};
	}
}
