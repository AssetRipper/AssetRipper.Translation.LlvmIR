using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed record class ReturnIfExceptionInfoNotNullInstruction(TypeSignature ReturnType, FieldDefinition Field) : Instruction
{
	public override bool StackHeightDependent => true;
	public override int PopCount => 0;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		CilInstructionLabel defaultLabel = new();

		// If exception info is not null
		instructions.Add(CilOpCodes.Ldsfld, Field);
		instructions.Add(CilOpCodes.Brfalse, defaultLabel);

		// An exception was thrown during the call, so we need to exit the function
		instructions.AddDefaultValue(ReturnType); // Does nothing if the return type is void
		instructions.Add(CilOpCodes.Ret);

		defaultLabel.Instruction = instructions.Add(CilOpCodes.Nop);
	}

	public static ReturnIfExceptionInfoNotNullInstruction Create(TypeSignature returnType, ModuleContext module)
	{
		FieldDefinition field = module.InjectedTypes[typeof(ExceptionInfo)].GetFieldByName(nameof(ExceptionInfo.Current))
			?? throw new NullReferenceException(nameof(field));
		return new ReturnIfExceptionInfoNotNullInstruction(returnType, field);
	}

	public bool Equals(ReturnIfExceptionInfoNotNullInstruction? other)
	{
		if (other is null)
		{
			return false;
		}
		return SignatureComparer.Default.Equals(ReturnType, other.ReturnType) && SignatureComparer.Default.Equals(Field, other.Field);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(SignatureComparer.Default.GetHashCode(ReturnType), SignatureComparer.Default.GetHashCode(Field));
	}
}
