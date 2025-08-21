using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class ReturnIfExceptionInfoNotNullInstruction(TypeSignature returnType, FieldDefinition field) : Instruction
{
	public override int PopCount => 0;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		CilInstructionLabel defaultLabel = new();

		// If exception info is not null
		instructions.Add(CilOpCodes.Ldsfld, field);
		instructions.Add(CilOpCodes.Brfalse, defaultLabel);

		// An exception was thrown during the call, so we need to exit the function
		instructions.AddDefaultValue(returnType); // Does nothing if the return type is void
		instructions.Add(CilOpCodes.Ret);

		defaultLabel.Instruction = instructions.Add(CilOpCodes.Nop);
	}

	public static ReturnIfExceptionInfoNotNullInstruction Create(TypeSignature returnType, ModuleContext module)
	{
		FieldDefinition field = module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current))
			?? throw new NullReferenceException(nameof(field));
		return new ReturnIfExceptionInfoNotNullInstruction(returnType, field);
	}
}
