using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed class ClearStackFrameInstruction(FunctionContext function) : Instruction
{
	public FunctionContext Function { get; } = function;
	public override int PopCount => 0;
	public override int PushCount => 0;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Debug.Assert(Function.NeedsStackFrame);
		Debug.Assert(Function.StackFrameVariable is not null);
		TypeDefinition stackFrameListType = Function.Module.InjectedTypes[typeof(StackFrameList)];
		instructions.Add(CilOpCodes.Ldsflda, stackFrameListType.GetFieldByName(nameof(StackFrameList.Current)));
		instructions.Add(CilOpCodes.Ldloc, Function.StackFrameVariable);
		instructions.Add(CilOpCodes.Call, stackFrameListType.Methods.Single(m => m.Name == nameof(StackFrameList.Clear) && m.IsPublic));
	}
}
