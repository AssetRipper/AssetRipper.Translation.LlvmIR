using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class ReturnInstructionContext : InstructionContext
{
	internal ReturnInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length is 0 or 1);
		ResultTypeSignature = module.Definition.CorLibTypeFactory.Void;
	}
	public bool HasReturnValue => Operands.Length == 1;
	public LLVMValueRef ResultOperand => HasReturnValue ? Operands[0] : default;

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		if (HasReturnValue)
		{
			Module.LoadValue(instructions, ResultOperand);
		}
		instructions.Add(CilOpCodes.Ret);
	}
}
