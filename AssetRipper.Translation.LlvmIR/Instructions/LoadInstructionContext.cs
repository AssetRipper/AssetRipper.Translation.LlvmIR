using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class LoadInstructionContext : InstructionContext
{
	internal LoadInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMLoad);
		Debug.Assert(Operands.Length == 1);
	}

	public LLVMValueRef SourceOperand => Operands[0];

	public InstructionContext? SourceInstruction { get; set; }

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		if (SourceInstruction is AllocaInstructionContext allocaInstruction)
		{
			Debug.Assert(Function is not null);

			if (SignatureComparer.Default.Equals(allocaInstruction.DataTypeSignature, ResultTypeSignature))
			{
				if (allocaInstruction.DataLocal is not null)
				{
					instructions.Add(CilOpCodes.Ldloc, allocaInstruction.DataLocal);
				}
				else
				{
					Debug.Assert(allocaInstruction.DataField is not null);
					Function.AddLocalVariablesRef(instructions);
					instructions.Add(CilOpCodes.Ldfld, allocaInstruction.DataField);
				}
			}
			else
			{
				if (allocaInstruction.DataLocal is not null)
				{
					instructions.Add(CilOpCodes.Ldloca, allocaInstruction.DataLocal);
				}
				else
				{
					Debug.Assert(allocaInstruction.DataField is not null);
					Function.AddLocalVariablesRef(instructions);
					instructions.Add(CilOpCodes.Ldflda, allocaInstruction.DataField);
				}
				instructions.AddLoadIndirect(ResultTypeSignature);
			}
		}
		else if (IsSourceGlobalVariable(out GlobalVariableContext? globalVariable))
		{
			instructions.Add(CilOpCodes.Call, globalVariable.DataGetMethod);
		}
		else
		{
			Module.LoadValue(instructions, SourceOperand);
			instructions.AddLoadIndirect(ResultTypeSignature);
		}

		AddStore(instructions);
	}

	private bool IsSourceGlobalVariable([NotNullWhen(true)] out GlobalVariableContext? globalVariable)
	{
		globalVariable = Module.GlobalVariables.TryGetValue(SourceOperand);
		return globalVariable is not null && SignatureComparer.Default.Equals(globalVariable.DataGetMethod.Signature?.ReturnType, ResultTypeSignature);
	}
}
