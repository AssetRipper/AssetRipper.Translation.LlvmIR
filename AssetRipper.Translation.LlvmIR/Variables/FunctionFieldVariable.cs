using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Variables;

internal class FunctionFieldVariable(TypeSignature variableType, FunctionContext function) : IVariable
{
	public bool IsTemporary => true;
	public TypeSignature VariableType { get; } = variableType;
	public FunctionContext Function { get; } = function;
	private FieldDefinition? DataField { get; set; }

	private FieldDefinition GetOrCreateField()
	{
		if (DataField is not null)
		{
			return DataField;
		}

		Debug.Assert(Function.NeedsStackFrame);
		Debug.Assert(Function.LocalVariablesType is not null);
		DataField = new FieldDefinition($"field_{Function.LocalVariablesType.Fields.Count}", FieldAttributes.Public, VariableType);
		Function.LocalVariablesType.Fields.Add(DataField);
		return DataField;
	}

	public void AddLoad(CilInstructionCollection instructions)
	{
		Function.AddLocalVariablesPointer(instructions);
		instructions.Add(CilOpCodes.Ldfld, GetOrCreateField());
	}

	public void AddStore(CilInstructionCollection instructions)
	{
		CilLocalVariable tempVariable = instructions.AddLocalVariable(VariableType);
		instructions.Add(CilOpCodes.Stloc, tempVariable);
		Function.AddLocalVariablesPointer(instructions);
		instructions.Add(CilOpCodes.Ldloc, tempVariable);
		instructions.Add(CilOpCodes.Stfld, GetOrCreateField());
	}

	public void AddLoadAddress(CilInstructionCollection instructions)
	{
		Function.AddLocalVariablesPointer(instructions);
		instructions.Add(CilOpCodes.Ldflda, GetOrCreateField());
		instructions.Add(CilOpCodes.Conv_U);
	}

	public void AddStoreDefault(CilInstructionCollection instructions)
	{
		if (VariableType is not CorLibTypeSignature and ({ IsValueType: true } or GenericParameterSignature))
		{
			Function.AddLocalVariablesPointer(instructions);
			instructions.Add(CilOpCodes.Ldflda, GetOrCreateField());
			instructions.Add(CilOpCodes.Initobj, VariableType.ToTypeDefOrRef());
		}
		else
		{
			Function.AddLocalVariablesPointer(instructions);
			instructions.AddDefaultValue(VariableType);
			instructions.Add(CilOpCodes.Stfld, GetOrCreateField());
		}
	}

	public static LocalVariable CreateFromInstruction(LLVMValueRef instruction, ModuleContext module)
	{
		return new(module.GetTypeSignature(instruction));
	}
}
