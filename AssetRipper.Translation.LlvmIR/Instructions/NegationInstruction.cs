using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed record class NegationInstruction : Instruction
{
	public static NegationInstruction Instance { get; } = new();
	public override int PopCount => 1;
	public override int PushCount => 1;
	public override void AddInstructions(CilInstructionCollection instructions)
	{
		instructions.Add(CilOpCodes.Neg);
	}
	internal static Instruction Create(TypeSignature type, ModuleContext module)
	{
		if (type is CorLibTypeSignature)
		{
			return Instance;
		}
		else if (type is TypeDefOrRefSignature)
		{
			TypeDefinition typeDef = type.Resolve() ?? throw new NullReferenceException(nameof(typeDef));
			if (module.InlineArrayTypes.TryGetValue(typeDef, out InlineArrayContext? arrayType))
			{
				arrayType.GetUltimateElementType(out TypeSignature elementType, out _);
				IMethodDescriptor negationMethod = module.InlineArrayNumericHelperType.Methods.First(m => m.Name == nameof(InlineArrayNumericHelper.Negate))
					.MakeGenericInstanceMethod(type, elementType);
				return new CallInstruction(negationMethod);
			}
			else
			{
				IMethodDescriptor negationMethod = module.NumericHelperType.Methods.First(m => m.Name == nameof(NumericHelper.Negate))
					.MakeGenericInstanceMethod(type);
				return new CallInstruction(negationMethod);
			}
		}
		else
		{
			throw new NotSupportedException($"Unsupported type for negation: {type}");
		}
	}
}
