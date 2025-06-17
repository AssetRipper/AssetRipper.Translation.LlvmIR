using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

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
		if (Function!.MightThrowAnException)
		{
			Debug.Assert(Function.StackFrameVariable is not null);
			TypeDefinition stackFrameListType = Module.InjectedTypes[typeof(StackFrameList)];

			instructions.Add(CilOpCodes.Ldsflda, stackFrameListType.GetFieldByName(nameof(StackFrameList.Current)));
			instructions.Add(CilOpCodes.Ldloc, Function.StackFrameVariable);
			instructions.Add(CilOpCodes.Call, stackFrameListType.Methods.Single(m => m.Name == nameof(StackFrameList.Clear) && m.IsPublic));
		}
		instructions.Add(CilOpCodes.Ret);
	}
}
