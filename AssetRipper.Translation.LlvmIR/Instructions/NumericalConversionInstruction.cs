using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

internal sealed record class NumericalConversionInstruction : Instruction
{
	public override int PopCount => 1;
	public override int PushCount => 1;
	public required LLVMOpcode Opcode { get; init; }
	public required TypeSignature SourceTypeSignature { get; init; }
	public required TypeSignature ResultTypeSignature { get; init; }
	public bool NoSignedWrap { get; init; }
	public bool NoUnsignedWrap { get; init; }

	public static Instruction Create(LLVMValueRef instruction, ModuleContext module)
	{
		Debug.Assert(Supported(instruction.GetOpcode()), $"Unsupported numerical conversion instruction: {instruction.GetOpcode()}");

		return new NumericalConversionInstruction
		{
			Opcode = instruction.GetOpcode(),
			SourceTypeSignature = module.GetTypeSignature(instruction.GetOperand(0)),
			ResultTypeSignature = module.GetTypeSignature(instruction),
			NoSignedWrap = LibLLVMSharp.InstructionHasNoSignedWrap(instruction),
			NoUnsignedWrap = LibLLVMSharp.InstructionHasNoUnsignedWrap(instruction),
		};
	}

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

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		if (ResultTypeSignature is CorLibTypeSignature { ElementType: ElementType.Boolean })
		{
			if (SourceTypeSignature is CorLibTypeSignature { ElementType: not ElementType.R4 and not ElementType.R8 })
			{
				if (SourceTypeSignature.ElementType is ElementType.I8 or ElementType.U8)
				{
					instructions.Add(CilOpCodes.Conv_I4);
				}
				instructions.Add(CilOpCodes.Ldc_I4_1);
				instructions.Add(CilOpCodes.And);
				instructions.Add(CilOpCodes.Ldc_I4_1);
				instructions.Add(CilOpCodes.Ceq);
			}
			else
			{
				throw new NotSupportedException($"Cannot convert {SourceTypeSignature} to boolean.");
			}
			return;
		}

		ModuleDefinition module = instructions.Owner.Owner!.DeclaringModule!;

		switch (Opcode)
		{
			// Signed integers
			case LLVMOpcode.LLVMSExt or LLVMOpcode.LLVMTrunc or LLVMOpcode.LLVMFPToSI:
				{
					if (ResultTypeSignature is CorLibTypeSignature c && SourceTypeSignature is CorLibTypeSignature)
					{
						instructions.Add(c.ElementType switch
						{
							ElementType.I1 or ElementType.U1 => CilOpCodes.Conv_I1,
							ElementType.I2 or ElementType.U2 or ElementType.Char => CilOpCodes.Conv_I2,
							ElementType.I4 or ElementType.U4 => CilOpCodes.Conv_I4,
							ElementType.I8 or ElementType.U8 => CilOpCodes.Conv_I8,
							ElementType.I or ElementType.U => CilOpCodes.Conv_I,
							_ => throw new NotSupportedException(),
						});
					}
					else if (ResultTypeSignature is CorLibTypeSignature && SourceTypeSignature is TypeDefOrRefSignature)
					{
						MethodDefinition conversionMethod = GetConversionMethod(ResolveTypeSignature(SourceTypeSignature), SourceTypeSignature, ResultTypeSignature, NoSignedWrap);
						instructions.Add(CilOpCodes.Call, module.DefaultImporter.ImportMethod(conversionMethod));
					}
					else if (ResultTypeSignature is TypeDefOrRefSignature && SourceTypeSignature is CorLibTypeSignature)
					{
						MethodDefinition conversionMethod = GetConversionMethod(ResolveTypeSignature(ResultTypeSignature), SourceTypeSignature, ResultTypeSignature, NoSignedWrap);
						instructions.Add(CilOpCodes.Call, module.DefaultImporter.ImportMethod(conversionMethod));
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

					if (ResultTypeSignature is CorLibTypeSignature c && SourceTypeSignature is CorLibTypeSignature or PointerTypeSignature)
					{
						AddFlipToUnsigned(instructions);
						instructions.Add(c.ElementType switch
						{
							ElementType.I1 or ElementType.U1 => CilOpCodes.Conv_U1,
							ElementType.I2 or ElementType.U2 or ElementType.Char => CilOpCodes.Conv_U2,
							ElementType.I4 or ElementType.U4 => CilOpCodes.Conv_U4,
							ElementType.I8 or ElementType.U8 => CilOpCodes.Conv_U8,
							ElementType.I or ElementType.U => CilOpCodes.Conv_U,
							_ => throw new NotSupportedException(),
						});
					}
					else if (ResultTypeSignature is PointerTypeSignature && SourceTypeSignature is CorLibTypeSignature)
					{
						instructions.Add(CilOpCodes.Conv_U);
					}
					else if (ResultTypeSignature is CorLibTypeSignature && SourceTypeSignature is TypeDefOrRefSignature)
					{
						AddFlipToUnsigned(instructions);
						TypeSignature unsignedSource = GetUnsignedType(SourceTypeSignature, module);
						TypeSignature unsignedResult = GetUnsignedType(ResultTypeSignature, module);
						MethodDefinition conversionMethod = GetConversionMethod(ResolveTypeSignature(unsignedSource), unsignedSource, unsignedResult, NoUnsignedWrap);
						instructions.Add(CilOpCodes.Call, module.DefaultImporter.ImportMethod(conversionMethod));
					}
					else if (ResultTypeSignature is TypeDefOrRefSignature && SourceTypeSignature is CorLibTypeSignature)
					{
						AddFlipToUnsigned(instructions);
						TypeSignature unsignedSource = GetUnsignedType(SourceTypeSignature, module);
						TypeSignature unsignedResult = GetUnsignedType(ResultTypeSignature, module);
						MethodDefinition conversionMethod = GetConversionMethod(ResolveTypeSignature(unsignedResult), unsignedSource, unsignedResult, NoUnsignedWrap);
						instructions.Add(CilOpCodes.Call, module.DefaultImporter.ImportMethod(conversionMethod));

						// We need to convert the result back to signed for type safety
						MethodDefinition backToSigned = GetConversionMethod(ResolveTypeSignature(unsignedResult), unsignedResult, ResultTypeSignature);
						instructions.Add(CilOpCodes.Call, module.DefaultImporter.ImportMethod(backToSigned));
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
					if (ResultTypeSignature is CorLibTypeSignature c && SourceTypeSignature is CorLibTypeSignature)
					{
						switch (c.ElementType)
						{
							case ElementType.R4:
								instructions.Add(CilOpCodes.Conv_R4);
								break;
							case ElementType.R8:
								instructions.Add(CilOpCodes.Conv_R8);
								break;
							default:
								throw new NotSupportedException();
						}
					}
					else if (ResultTypeSignature is CorLibTypeSignature && SourceTypeSignature is TypeDefOrRefSignature)
					{
						MethodDefinition conversionMethod = GetConversionMethod(ResolveTypeSignature(SourceTypeSignature), SourceTypeSignature, ResultTypeSignature);
						instructions.Add(CilOpCodes.Call, instructions.Owner.Owner!.DeclaringModule!.DefaultImporter.ImportMethod(conversionMethod));
					}
					else if (ResultTypeSignature is TypeDefOrRefSignature && SourceTypeSignature is CorLibTypeSignature)
					{
						MethodDefinition conversionMethod = GetConversionMethod(ResolveTypeSignature(ResultTypeSignature), SourceTypeSignature, ResultTypeSignature);
						instructions.Add(CilOpCodes.Call, instructions.Owner.Owner!.DeclaringModule!.DefaultImporter.ImportMethod(conversionMethod));
					}
					else
					{
						throw new NotSupportedException();
					}
				}
				break;
		}
	}

	private void AddFlipToUnsigned(CilInstructionCollection instructions)
	{
		if (SourceTypeSignature is CorLibTypeSignature c)
		{
			switch (c.ElementType)
			{
				case ElementType.I1:
					instructions.Add(CilOpCodes.Conv_U1);
					break;
				case ElementType.I2:
					instructions.Add(CilOpCodes.Conv_U2);
					break;
				case ElementType.I4:
					instructions.Add(CilOpCodes.Conv_U4);
					break;
				case ElementType.I8:
					instructions.Add(CilOpCodes.Conv_U8);
					break;
				case ElementType.I:
					instructions.Add(CilOpCodes.Conv_U);
					break;
			}
		}
		else if (SourceTypeSignature is TypeDefOrRefSignature { Namespace: "System", Name: "Int128" })
		{
			TypeDefinition sourceTypeDefinition = ResolveTypeSignature(SourceTypeSignature);
			TypeSignature unsignedType = GetUnsignedType(SourceTypeSignature, instructions.Owner.Owner!.DeclaringModule!);
			MethodDefinition conversionMethod = GetConversionMethod(sourceTypeDefinition, SourceTypeSignature, unsignedType);
			instructions.Add(CilOpCodes.Call, instructions.Owner.Owner!.DeclaringModule!.DefaultImporter.ImportMethod(conversionMethod));
		}
	}

	private static TypeDefinition ResolveTypeSignature(TypeSignature typeSignature)
	{
		return typeSignature.Resolve() ?? throw new NotSupportedException($"Cannot resolve type: {typeSignature}");
	}

	private static MethodDefinition GetConversionMethod(TypeDefinition typeDefinition, TypeSignature sourceTypeSignature, TypeSignature resultTypeSignature, bool @checked = false)
	{
		string explicitMethodName = @checked ? "op_CheckedExplicit" : "op_Explicit";
		foreach (MethodDefinition method in typeDefinition.Methods)
		{
			if (method.Name != "op_Implicit" && method.Name != explicitMethodName)
			{
				continue;
			}

			if (method.Parameters.Count == 1 &&
				SignatureComparer.Default.Equals(method.Parameters[0].ParameterType, sourceTypeSignature) &&
				SignatureComparer.Default.Equals(method.Signature?.ReturnType, resultTypeSignature))
			{
				return method;
			}
		}
		throw new NotSupportedException($"Cannot find conversion method for {typeDefinition.FullName} from {sourceTypeSignature} to {resultTypeSignature}.");
	}

	private static TypeSignature GetUnsignedType(TypeSignature type, ModuleDefinition module) => type switch
	{
		CorLibTypeSignature c => c.ElementType switch
		{
			ElementType.I1 => module.CorLibTypeFactory.Byte,
			ElementType.I2 => module.CorLibTypeFactory.UInt16,
			ElementType.I4 => module.CorLibTypeFactory.UInt32,
			ElementType.I8 => module.CorLibTypeFactory.UInt64,
			ElementType.I => module.CorLibTypeFactory.UIntPtr,
			_ => type,
		},
		PointerTypeSignature => type,
		TypeDefOrRefSignature { Namespace: "System", Name: "Int128" } => module.DefaultImporter.ImportTypeSignature(typeof(UInt128)),
		_ => type,
	};
}
