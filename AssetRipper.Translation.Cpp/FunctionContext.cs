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
	public Dictionary<LLVMValueRef, CilLocalVariable> Locals { get; } = new();
	public Dictionary<LLVMBasicBlockRef, CilInstructionLabel> Labels { get; } = new();
	public Dictionary<LLVMValueRef, Parameter> Parameters { get; } = new();

	public void LoadLocalOrConstant(LLVMValueRef operand, out TypeSignature typeSignature)
	{
		switch (operand.Kind)
		{
			case LLVMValueKind.LLVMConstantIntValueKind:
				{
					long value = operand.ConstIntSExt;
					if (value is <= int.MaxValue and >= int.MinValue)
					{
						Instructions.Add(CilOpCodes.Ldc_I4, (int)value);
					}
					else
					{
						Instructions.Add(CilOpCodes.Ldc_I8, value);
					}
					typeSignature = Module.GetTypeSignature(operand.TypeOf);
				}
				break;
			case LLVMValueKind.LLVMInstructionValueKind:
				{
					CilLocalVariable local = Locals[operand];
					Instructions.Add(CilOpCodes.Ldloc, local);
					typeSignature = local.VariableType;
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
			LLVMValueKind.LLVMInstructionValueKind => Locals[operand].VariableType,
			_ => throw new NotSupportedException(),
		};
	}
}
