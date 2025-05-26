using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class CatchSwitchInstructionContext : InstructionContext
{
	internal CatchSwitchInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length >= 1);
	}

	public CilInstructionLabel TryStartLabel { get; } = new();
	public CilInstructionLabel TryEndLabel { get; } = new();
	public LLVMValueRef ParentRef => Operands[0];
	public bool HasDefaultUnwind => false;
	public LLVMBasicBlockRef DefaultUnwindTargetRef => HasDefaultUnwind ? Operands[^1].AsBasicBlock() : default;
	public BasicBlockContext? DefaultUnwindTarget => HasDefaultUnwind ? Function?.BasicBlockLookup[DefaultUnwindTargetRef] : null;
	public ReadOnlySpan<LLVMValueRef> Handlers => HasDefaultUnwind ? Operands.AsSpan()[1..^1] : Operands.AsSpan()[1..];
	public IReadOnlyList<CatchPadInstructionContext> CatchPads
	{
		get
		{
			Debug.Assert(Function is not null);
			CatchPadInstructionContext[] catchPads = new CatchPadInstructionContext[Handlers.Length];
			for (int i = 0; i < Handlers.Length; i++)
			{
				BasicBlockContext handlerBlock = Function.BasicBlockLookup[Handlers[i].AsBasicBlock()];
				catchPads[i] = (CatchPadInstructionContext)handlerBlock.Instructions[0];
			}
			return catchPads;
		}
	}
	public BasicBlockContext? UltimateTarget => CatchPads[0]?.CatchReturn.TargetBlock;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		throw new NotImplementedException();
		foreach (CatchPadInstructionContext catchPad in CatchPads)
		{
			if (catchPad.HasFilter)
			{
				instructions.Owner.ExceptionHandlers.Add(new CilExceptionHandler
				{
					TryStart = TryStartLabel,
					TryEnd = TryEndLabel,
					HandlerStart = catchPad.HandlerStartLabel,
					HandlerEnd = catchPad.HandlerEndLabel,
					FilterStart = catchPad.FilterStartLabel,
					HandlerType = CilExceptionHandlerType.Filter,
					ExceptionType = Module.Definition.CorLibTypeFactory.Object.ToTypeDefOrRef(),
				});
			}
			else
			{
				instructions.Owner.ExceptionHandlers.Add(new CilExceptionHandler
				{
					TryStart = TryStartLabel,
					TryEnd = TryEndLabel,
					HandlerStart = catchPad.HandlerStartLabel,
					HandlerEnd = catchPad.HandlerEndLabel,
					HandlerType = CilExceptionHandlerType.Exception,
					ExceptionType = Module.Definition.CorLibTypeFactory.Object.ToTypeDefOrRef(),
				});
			}
		}
	}
}
