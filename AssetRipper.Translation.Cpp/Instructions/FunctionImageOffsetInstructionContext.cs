using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class FunctionImageOffsetInstructionContext : LiftedInstructionContext
{
	internal FunctionImageOffsetInstructionContext(LLVMValueRef instruction, ModuleContext module, FunctionContext targetFunction) : base(instruction, module)
	{
		TargetFunction = targetFunction;
		ResultTypeSignature = Module.Definition.CorLibTypeFactory.Int32;
	}
	public FunctionContext TargetFunction { get; }
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Module.LoadValue(instructions, TargetFunction.Function);
		instructions.Add(CilOpCodes.Call, Module.InjectedTypes[typeof(PointerIndices)].GetMethodByName(nameof(PointerIndices.GetIndex)));
		AddStore(instructions);
	}
}
