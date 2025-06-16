using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
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
			LLVMOpcode.LLVMVAArg => new VAArgInstructionContext(instruction, module),
			LLVMOpcode.LLVMInvoke => new InvokeInstructionContext(instruction, module),
			LLVMOpcode.LLVMCatchSwitch => new CatchSwitchInstructionContext(instruction, module),
			LLVMOpcode.LLVMCatchPad => new CatchPadInstructionContext(instruction, module),
			LLVMOpcode.LLVMCatchRet => new CatchReturnInstructionContext(instruction, module),
			LLVMOpcode.LLVMCleanupPad => new CleanupPadInstructionContext(instruction, module),
			LLVMOpcode.LLVMCleanupRet => new CleanupReturnInstructionContext(instruction, module),
			_ when UnaryMathInstructionContext.Supported(instruction.GetOpcode()) => new UnaryMathInstructionContext(instruction, module),
			_ when BinaryMathInstructionContext.Supported(instruction.GetOpcode()) => new BinaryMathInstructionContext(instruction, module),
			_ when NumericConversionInstructionContext.Supported(instruction.GetOpcode()) => new NumericConversionInstructionContext(instruction, module),
			_ => new GenericInstructionContext(instruction, module),
		};
	}

	public LLVMOpcode Opcode => Instruction.GetOpcode();
	public bool NoSignedWrap => LibLLVMSharp.InstructionHasNoSignedWrap(Instruction);
	public bool NoUnsignedWrap => LibLLVMSharp.InstructionHasNoUnsignedWrap(Instruction);
	public int Index => Function?.Instructions.IndexOf(this) ?? -1;
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
	public FieldDefinition? ResultField { get; set; }

	[MemberNotNullWhen(true, nameof(ResultTypeSignature))]
	public bool HasResult => ResultTypeSignature is not null and not CorLibTypeSignature { ElementType: ElementType.Void };

	private string GetDebuggerDisplay()
	{
		return Instruction.ToString();
	}

	public virtual void CreateLocal(CilInstructionCollection instructions)
	{
		if (!HasResult)
		{
		}
		else if (Function is null or { MightThrowAnException: false })
		{
			ResultLocal = instructions.AddLocalVariable(ResultTypeSignature);
		}
		else
		{
			Debug.Assert(Function.LocalVariablesType is not null);
			ResultField = new FieldDefinition($"Instruction_{Index}", FieldAttributes.Public, ResultTypeSignature);
			Function.LocalVariablesType.Fields.Add(ResultField);
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

	public void AddLoad(CilInstructionCollection instructions)
	{
		if (ResultLocal is not null)
		{
			instructions.Add(CilOpCodes.Ldloc, ResultLocal);
		}
		else if (ResultField is not null)
		{
			Debug.Assert(Function is not null);
			Function.AddLocalVariablesRef(instructions);
			instructions.Add(CilOpCodes.Ldfld, ResultField);
		}
		else
		{
			Debug.Assert(false, "Result local and field are both null");
		}
	}

	public void AddStore(CilInstructionCollection instructions)
	{
		if (ResultLocal is not null)
		{
			instructions.Add(CilOpCodes.Stloc, ResultLocal);
		}
		else if (ResultField is not null)
		{
			Debug.Assert(Function is not null);
			CilLocalVariable tempLocal = instructions.AddLocalVariable(ResultTypeSignature);
			instructions.Add(CilOpCodes.Stloc, tempLocal);
			Function.AddLocalVariablesRef(instructions);
			instructions.Add(CilOpCodes.Ldloc, tempLocal);
			instructions.Add(CilOpCodes.Stfld, ResultField);
		}
		else
		{
			Debug.Assert(false, "Result local and field are both null");
		}
	}

	protected void AddLoadIfBranchingToPhi(CilInstructionCollection instructions, BasicBlockContext targetBlock)
	{
		if (!TargetBlockStartsWithPhi(targetBlock))
		{
			return;
		}

		ThrowIfBasicBlockIsNull();
		ThrowIfFunctionIsNull();

		foreach (InstructionContext instruction in targetBlock.Instructions)
		{
			if (instruction is PhiInstructionContext phiInstruction)
			{
				LLVMValueRef phiOperand = phiInstruction.GetOperandForOriginBlock(BasicBlock);
				Module.LoadValue(instructions, phiOperand);
				phiInstruction.AddStore(instructions);
			}
			else
			{
				break;
			}
		}
	}

	protected static bool TargetBlockStartsWithPhi(BasicBlockContext targetBlock)
	{
		return targetBlock.Instructions.Count > 0 && targetBlock.Instructions[0] is PhiInstructionContext;
	}
}
