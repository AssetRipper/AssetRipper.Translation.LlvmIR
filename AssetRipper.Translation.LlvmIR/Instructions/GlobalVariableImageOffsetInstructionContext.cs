using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class GlobalVariableImageOffsetInstructionContext : LiftedInstructionContext
{
	internal GlobalVariableImageOffsetInstructionContext(LLVMValueRef instruction, ModuleContext module, GlobalVariableContext targetVariable) : base(instruction, module)
	{
		TargetVariable = targetVariable;
		ResultTypeSignature = Module.Definition.CorLibTypeFactory.Int32;
	}
	public GlobalVariableContext TargetVariable { get; }
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Module.LoadValue(instructions, TargetVariable.GlobalVariable);
		instructions.Add(CilOpCodes.Call, Module.InjectedTypes[typeof(PointerIndices)].GetMethodByName(nameof(PointerIndices.GetIndex)));
		AddStore(instructions);
	}
}
