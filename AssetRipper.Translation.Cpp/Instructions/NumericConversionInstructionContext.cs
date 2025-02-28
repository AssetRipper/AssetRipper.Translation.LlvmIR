using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.CIL;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class NumericConversionInstructionContext : InstructionContext
{
	internal NumericConversionInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 1);
	}
	public LLVMValueRef Operand => Operands[0];
	public static bool Supported(LLVMOpcode opcode) => opcode switch
	{
		LLVMOpcode.LLVMZExt => true,
		LLVMOpcode.LLVMSExt => true,
		LLVMOpcode.LLVMTrunc => true,
		LLVMOpcode.LLVMFPExt => true,
		LLVMOpcode.LLVMFPTrunc => true,
		LLVMOpcode.LLVMPtrToInt => true,
		LLVMOpcode.LLVMIntToPtr => true,
		LLVMOpcode.LLVMSIToFP => true,
		LLVMOpcode.LLVMUIToFP => true,
		LLVMOpcode.LLVMFPToSI => true,
		LLVMOpcode.LLVMFPToUI => true,
		_ => false,
	};

	public void AddConversion(CilInstructionCollection cilInstructions)
	{
		switch (Opcode)
		{
			// Signed integers
			case LLVMOpcode.LLVMSExt or LLVMOpcode.LLVMTrunc or LLVMOpcode.LLVMFPToSI:
				{
					if (ResultTypeSignature is CorLibTypeSignature c)
					{
						switch (c.ElementType)
						{
							case ElementType.I1 or ElementType.U1:
								cilInstructions.Add(CilOpCodes.Conv_I1);
								break;
							case ElementType.I2 or ElementType.U2 or ElementType.Char:
								cilInstructions.Add(CilOpCodes.Conv_I2);
								break;
							case ElementType.I4 or ElementType.U4:
								cilInstructions.Add(CilOpCodes.Conv_I4);
								break;
							case ElementType.I8 or ElementType.U8:
								cilInstructions.Add(CilOpCodes.Conv_I8);
								break;
							case ElementType.I or ElementType.U:
								cilInstructions.Add(CilOpCodes.Conv_I);
								break;
							case ElementType.Boolean:
								cilInstructions.AddDefaultValue(Module.GetTypeSignature(Operand.TypeOf));
								cilInstructions.Add(CilOpCodes.Ceq);
								break;
							default:
								throw new NotSupportedException();
						}
					}
					else
					{
						throw new NotSupportedException();
					}
				}
				break;

			// Unsigned integers
			case LLVMOpcode.LLVMZExt or LLVMOpcode.LLVMPtrToInt or LLVMOpcode.LLVMIntToPtr or LLVMOpcode.LLVMFPToUI:
				{
					if (ResultTypeSignature is CorLibTypeSignature c)
					{
						switch (c.ElementType)
						{
							case ElementType.I1 or ElementType.U1:
								cilInstructions.Add(CilOpCodes.Conv_U1);
								break;
							case ElementType.I2 or ElementType.U2 or ElementType.Char:
								cilInstructions.Add(CilOpCodes.Conv_U2);
								break;
							case ElementType.I4 or ElementType.U4:
								cilInstructions.Add(CilOpCodes.Conv_U4);
								break;
							case ElementType.I8 or ElementType.U8:
								cilInstructions.Add(CilOpCodes.Conv_U8);
								break;
							case ElementType.I or ElementType.U:
								cilInstructions.Add(CilOpCodes.Conv_U);
								break;
							case ElementType.Boolean:
								cilInstructions.AddDefaultValue(Module.GetTypeSignature(Operand.TypeOf));
								cilInstructions.Add(CilOpCodes.Ceq);
								break;
							default:
								throw new NotSupportedException();
						}
					}
					else if (ResultTypeSignature is PointerTypeSignature)
					{
						cilInstructions.Add(CilOpCodes.Conv_U);
					}
					else
					{
						throw new NotSupportedException();
					}
				}
				break;

			// Floating point
			case LLVMOpcode.LLVMFPExt or LLVMOpcode.LLVMFPTrunc or LLVMOpcode.LLVMSIToFP or LLVMOpcode.LLVMUIToFP:
				{
					if (ResultTypeSignature is CorLibTypeSignature c)
					{
						switch (c.ElementType)
						{
							case ElementType.R4:
								cilInstructions.Add(CilOpCodes.Conv_R4);
								break;
							case ElementType.R8:
								cilInstructions.Add(CilOpCodes.Conv_R8);
								break;
							default:
								throw new NotSupportedException();
						}
					}
					else
					{
						throw new NotSupportedException();
					}
				}
				break;
		}
	}

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Module.LoadValue(instructions, Operand);
		AddConversion(instructions);
		instructions.Add(CilOpCodes.Stloc, GetLocalVariable());
	}
}
