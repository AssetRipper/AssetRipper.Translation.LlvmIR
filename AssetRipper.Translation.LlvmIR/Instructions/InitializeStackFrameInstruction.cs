using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed record class InitializeStackFrameInstruction(FunctionContext Function) : Instruction
{
	public override int PopCount => 0;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Debug.Assert(Function.NeedsStackFrame);

		TypeDefinition typeDefinition = new(
			null,
			"LocalVariables",
			TypeAttributes.NestedPrivate | TypeAttributes.SequentialLayout,
			Function.Module.Definition.DefaultImporter.ImportType(typeof(ValueType)));
		Function.DeclaringType.NestedTypes.Add(typeDefinition);

		Function.LocalVariablesType = typeDefinition;

		Function.StackFrameVariable = Function.Definition.CilMethodBody!.Instructions.AddLocalVariable(Function.Module.InjectedTypes[typeof(StackFrame)].ToTypeSignature());

		Function.LocalVariablesPointer = Function.Definition.CilMethodBody.Instructions.AddLocalVariable(typeDefinition.MakePointerType());

		TypeDefinition stackFrameListType = Function.Module.InjectedTypes[typeof(StackFrameList)];

		instructions.Add(CilOpCodes.Ldsflda, stackFrameListType.GetFieldByName(nameof(StackFrameList.Current)));
		instructions.Add(CilOpCodes.Call, stackFrameListType.GetMethodByName(nameof(StackFrameList.New)).MakeGenericInstanceMethod(Function.LocalVariablesType.ToTypeSignature()));
		instructions.Add(CilOpCodes.Stloc, Function.StackFrameVariable);

		instructions.Add(CilOpCodes.Ldloca, Function.StackFrameVariable);
		instructions.Add(CilOpCodes.Call, Function.Module.InjectedTypes[typeof(StackFrame)].GetMethodByName(nameof(StackFrame.GetLocalsPointer)).MakeGenericInstanceMethod(Function.LocalVariablesType.ToTypeSignature()));
		instructions.Add(CilOpCodes.Stloc, Function.LocalVariablesPointer);
	}
}
