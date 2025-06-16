using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CatchPadInstructionContext : InstructionContext
{
	internal CatchPadInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length >= 1);
		Debug.Assert(Operands[^1].IsACatchSwitchInst != default);
		ResultTypeSignature = module.InjectedTypes[typeof(ExceptionInfo)].ToTypeSignature();
	}

	public LLVMValueRef CatchSwitchRef => Operands[^1];
	public CatchSwitchInstructionContext? CatchSwitch => (CatchSwitchInstructionContext?)(Function?.InstructionLookup[CatchSwitchRef]);
	public ReadOnlySpan<LLVMValueRef> Arguments => Operands.AsSpan()[..^1];
	public CatchReturnInstructionContext CatchReturn
	{
		get
		{
			Debug.Assert(Function is not null);
			return Function.Instructions.OfType<CatchReturnInstructionContext>().Single(i => i.CatchPad == this);
		}
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		FieldDefinition field = Module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current));
		instructions.Add(CilOpCodes.Ldsfld, field);
		AddStore(instructions);
		instructions.Add(CilOpCodes.Ldnull);
		instructions.Add(CilOpCodes.Stsfld, field);
	}
}
