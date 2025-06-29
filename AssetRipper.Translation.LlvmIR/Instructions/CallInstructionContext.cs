using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class CallInstructionContext : BaseCallInstructionContext
{
	internal CallInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMCall);
		Debug.Assert(Operands.Length >= 1);
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		base.AddInstructions(instructions);

		Debug.Assert(Function is not null);
		if (CalledFunction is { MightThrowAnException: false} || !Function.MightThrowAnException)
		{
			return; // no need to handle exceptions
		}

		CilInstructionLabel defaultLabel = new();

		// If exception info is not null
		instructions.Add(CilOpCodes.Ldsfld, Module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current)));
		instructions.Add(CilOpCodes.Brfalse, defaultLabel);

		// An exception was thrown during the call, so we need to exit the function
		instructions.AddDefaultValue(Function.ReturnTypeSignature); // Does nothing if the return type is void
		instructions.Add(CilOpCodes.Ret);

		defaultLabel.Instruction = instructions.Add(CilOpCodes.Nop);
	}
}
