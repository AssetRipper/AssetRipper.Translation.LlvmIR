using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.Cpp.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.Cpp.Instructions;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal abstract class InstructionContext
{
	protected InstructionContext(LLVMValueRef instruction, ModuleContext module)
	{
		Instruction = instruction;
		Module = module;
		Operands = instruction.GetOperands();
		ResultTypeSignature = module.GetTypeSignature(instruction.TypeOf);
	}

	public static InstructionContext Create(LLVMValueRef instruction, ModuleContext module)
	{
		return instruction.GetOpcode() switch
		{
			LLVMOpcode.LLVMAlloca => new AllocaInstructionContext(instruction, module),
			LLVMOpcode.LLVMLoad => new LoadInstructionContext(instruction, module),
			LLVMOpcode.LLVMStore => new StoreInstructionContext(instruction, module),
			LLVMOpcode.LLVMCall => new CallInstructionContext(instruction, module),
			LLVMOpcode.LLVMICmp => new IntegerComparisonInstructionContext(instruction, module),
			LLVMOpcode.LLVMFCmp => new FloatComparisonInstructionContext(instruction, module),
			LLVMOpcode.LLVMBr => instruction.IsConditional
				? new ConditionalBranchInstructionContext(instruction, module)
				: new UnconditionalBranchInstructionContext(instruction, module),
			LLVMOpcode.LLVMRet => new ReturnInstructionContext(instruction, module),
			LLVMOpcode.LLVMPHI => new PhiInstructionContext(instruction, module),
			LLVMOpcode.LLVMGetElementPtr => new GetElementPointerInstructionContext(instruction, module),
			LLVMOpcode.LLVMSwitch => new SwitchBranchInstructionContext(instruction, module),
			LLVMOpcode.LLVMSelect => new SelectInstructionContext(instruction, module),
			LLVMOpcode.LLVMBitCast => new BitCastInstructionContext(instruction, module),
			_ when UnaryMathInstructionContext.Supported(instruction.GetOpcode()) => new UnaryMathInstructionContext(instruction, module),
			_ when BinaryMathInstructionContext.Supported(instruction.GetOpcode()) => new BinaryMathInstructionContext(instruction, module),
			_ when NumericConversionInstructionContext.Supported(instruction.GetOpcode()) => new NumericConversionInstructionContext(instruction, module),
			_ => new GenericInstructionContext(instruction, module),
		};
	}

	public LLVMOpcode Opcode => Instruction.GetOpcode();
	public bool NoSignedWrap => LibLLVMSharp.InstructionHasNoSignedWrap(Instruction);
	public bool NoUnsignedWrap => LibLLVMSharp.InstructionHasNoUnsignedWrap(Instruction);
	public LLVMValueRef Instruction { get; }
	public LLVMBasicBlockRef BasicBlockRef => Instruction.InstructionParent;
	public LLVMValueRef FunctionRef => BasicBlockRef.Parent;
	public BasicBlockContext? BasicBlock => Function?.BasicBlockLookup.TryGetValue(BasicBlockRef);
	public FunctionContext? Function => Module.Methods.TryGetValue(FunctionRef);
	public ModuleContext Module { get; }
	public LLVMValueRef[] Operands { get; }
	public List<InstructionContext> Loads { get; } = new();
	public List<InstructionContext> Stores { get; } = new();
	public List<InstructionContext> Accessors { get; } = new();
	public TypeSignature ResultTypeSignature { get; set; }
	public CilLocalVariable? ResultLocal { get; set; }

	[MemberNotNullWhen(true, nameof(ResultTypeSignature))]
	public bool HasResult => ResultTypeSignature is not null and not CorLibTypeSignature { ElementType: ElementType.Void };

	private string GetDebuggerDisplay()
	{
		return Instruction.ToString();
	}

	public CilLocalVariable GetLocalVariable() => ResultLocal ?? throw new NullReferenceException("Result local is null");

	public virtual void CreateLocal(CilInstructionCollection instructions)
	{
		if (HasResult)
		{
			ResultLocal = instructions.AddLocalVariable(ResultTypeSignature);
		}
	}

	public abstract void AddInstructions(CilInstructionCollection instructions);

	[MemberNotNull(nameof(BasicBlock))]
	[StackTraceHidden]
	protected void ThrowIfBasicBlockIsNull()
	{
		if (BasicBlock is null)
		{
			throw new InvalidOperationException("Basic block is null");
		}
	}

	[MemberNotNull(nameof(Function))]
	[StackTraceHidden]
	protected void ThrowIfFunctionIsNull()
	{
		if (Function is null)
		{
			throw new InvalidOperationException("Function is null");
		}
	}
}
