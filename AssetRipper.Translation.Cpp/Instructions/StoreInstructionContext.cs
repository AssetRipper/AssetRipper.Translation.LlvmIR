using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class StoreInstructionContext : InstructionContext
{
	internal StoreInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Opcode == LLVMOpcode.LLVMStore);
		Debug.Assert(Operands.Length == 2);
		ResultTypeSignature = module.Definition.CorLibTypeFactory.Void;
		StoreTypeSignature = module.GetTypeSignature(StoreType);
	}
	public LLVMValueRef SourceOperand => Operands[0];
	public LLVMValueRef DestinationOperand => Operands[1];
	public InstructionContext? DestinationInstruction { get; set; }
	public LLVMTypeRef StoreType => SourceOperand.TypeOf;
	public TypeSignature StoreTypeSignature { get; set; }

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		if (DestinationInstruction is AllocaInstructionContext allocaInstruction)
		{
			Debug.Assert(Function is not null);

			if (SignatureComparer.Default.Equals(allocaInstruction.DataTypeSignature, ResultTypeSignature))
			{
				if (allocaInstruction.DataLocal is not null)
				{
					Module.LoadValue(instructions, SourceOperand);
					instructions.Add(CilOpCodes.Stloc, allocaInstruction.DataLocal);
				}
				else
				{
					Debug.Assert(allocaInstruction.DataField is not null);
					Function.AddLocalVariablesRef(instructions);
					Module.LoadValue(instructions, SourceOperand);
					instructions.Add(CilOpCodes.Stfld, allocaInstruction.DataField);
				}
			}
			else
			{
				if (allocaInstruction.DataLocal is not null)
				{
					instructions.Add(CilOpCodes.Ldloca, allocaInstruction.DataLocal);
				}
				else
				{
					Debug.Assert(allocaInstruction.DataField is not null);
					Function.AddLocalVariablesRef(instructions);
					instructions.Add(CilOpCodes.Ldflda, allocaInstruction.DataField);
				}

				Module.LoadValue(instructions, SourceOperand);
				instructions.AddStoreIndirect(StoreTypeSignature);
			}
		}
		else
		{
			Module.LoadValue(instructions, DestinationOperand);
			Module.LoadValue(instructions, SourceOperand);
			instructions.AddStoreIndirect(StoreTypeSignature);
		}
	}
}
