using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal class VAArgInstructionContext : InstructionContext
{
	// On Windows, this doesn't get used because Clang optimizes va_list away.

	public VAArgInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 1);
	}

	public LLVMValueRef Operand => Operands[0];

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Module.LoadValue(instructions, Operand);
		instructions.Add(CilOpCodes.Call, Module.InstructionHelperType.Methods.First(m => m.Name == nameof(InstructionHelper.VAArg)));
		instructions.AddLoadIndirect(ResultTypeSignature);
		instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
	}
}
