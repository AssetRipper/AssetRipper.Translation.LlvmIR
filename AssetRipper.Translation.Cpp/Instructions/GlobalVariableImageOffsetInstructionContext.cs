using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp.Instructions;

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
		instructions.Add(CilOpCodes.Call, Module.PointerIndexType.GetMethodByName("GetIndex"));
		AddStore(instructions);
	}
}
