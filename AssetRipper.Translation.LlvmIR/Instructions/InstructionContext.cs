using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.LlvmIR.Instructions;

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
		if (TryMatchImageOffset(instruction, module, out FunctionContext? function, out GlobalVariableContext? variable))
		{
			if (function is not null)
			{
				return new FunctionImageOffsetInstructionContext(instruction, module, function);
			}
			else if (variable is not null)
			{
				return new GlobalVariableImageOffsetInstructionContext(instruction, module, variable);
			}
		}

		LLVMOpcode opcode = instruction.GetOpcode();
		return opcode switch
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
			_ when UnaryMathInstructionContext.Supported(opcode) => new UnaryMathInstructionContext(instruction, module),
			_ when BinaryMathInstructionContext.Supported(opcode) => new BinaryMathInstructionContext(instruction, module),
			_ when NumericConversionInstructionContext.Supported(opcode) => new NumericConversionInstructionContext(instruction, module),
			_ => new GenericInstructionContext(instruction, module),
		};

		static bool TryMatchImageOffset(LLVMValueRef instruction, ModuleContext module, out FunctionContext? function, out GlobalVariableContext? variable)
		{
			if (instruction.Kind is not LLVMValueKind.LLVMConstantExprValueKind)
			{
				return False(out function, out variable);
			}

			LLVMValueRef trunc = instruction;
			if (trunc.ConstOpcode is not LLVMOpcode.LLVMTrunc || trunc.TypeOf is not { Kind: LLVMTypeKind.LLVMIntegerTypeKind, IntWidth: 32 })
			{
				return False(out function, out variable);
			}

			LLVMValueRef sub = trunc.GetOperand(0);
			if (sub.ConstOpcode is not LLVMOpcode.LLVMSub || sub.TypeOf is not { Kind: LLVMTypeKind.LLVMIntegerTypeKind, IntWidth: 64 })
			{
				return False(out function, out variable);
			}

			LLVMValueRef ptrToInt_Left = sub.GetOperand(0);
			LLVMValueRef ptrToInt_Right = sub.GetOperand(1);
			if (ptrToInt_Left.ConstOpcode is not LLVMOpcode.LLVMPtrToInt || ptrToInt_Right.ConstOpcode is not LLVMOpcode.LLVMPtrToInt)
			{
				return False(out function, out variable);
			}

			LLVMValueRef imageBase = ptrToInt_Right.GetOperand(0);
			if (imageBase.Kind is not LLVMValueKind.LLVMGlobalVariableValueKind || imageBase.Name is not "__ImageBase")
			{
				return False(out function, out variable);
			}

			LLVMValueRef address = ptrToInt_Left.GetOperand(0);
			if (address.Kind is LLVMValueKind.LLVMFunctionValueKind)
			{
				function = module.Methods[address];
				variable = null;
				return true;
			}
			else if (address.Kind is LLVMValueKind.LLVMGlobalVariableValueKind)
			{
				variable = module.GlobalVariables[address];
				function = null;
				return true;
			}
			else
			{
				return False(out function, out variable);
			}

			static bool False(out FunctionContext? function, out GlobalVariableContext? variable)
			{
				function = null;
				variable = null;
				return false;
			}
		}
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

	[MemberNotNullWhen(true, nameof(ResultTypeSignature))]
	public bool HasResult => ResultTypeSignature is not null and not CorLibTypeSignature { ElementType: ElementType.Void };

	private string GetDebuggerDisplay()
	{
		return Instruction.ToString();
	}

	public virtual void CreateLocal(CilInstructionCollection instructions)
	{
		if (HasResult && Instruction.IsUsed())
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

	public void AddLoad(CilInstructionCollection instructions)
	{
		Debug.Assert(ResultLocal is not null);
		instructions.Add(CilOpCodes.Ldloc, ResultLocal);
	}

	public void AddStore(CilInstructionCollection instructions)
	{
		if (ResultLocal is not null)
		{
			Debug.Assert(Instruction.IsUsed());
			instructions.Add(CilOpCodes.Stloc, ResultLocal);
		}
		else
		{
			Debug.Assert(!Instruction.IsUsed());
			instructions.Add(CilOpCodes.Pop);
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
