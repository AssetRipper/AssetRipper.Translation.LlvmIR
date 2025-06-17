using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class CleanupPadInstructionContext : InstructionContext
{
	internal CleanupPadInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 1);
		ResultTypeSignature = module.InjectedTypes[typeof(ExceptionInfo)].ToTypeSignature();
	}

	public LLVMValueRef Operand => Operands[0];
	public bool IsIndependent => Operand.IsAConstantTokenNone != default;

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		FieldDefinition field = Module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current));
		instructions.Add(CilOpCodes.Ldsfld, field);
		AddStore(instructions);
		instructions.Add(CilOpCodes.Ldnull);
		instructions.Add(CilOpCodes.Stsfld, field);
	}
}
