using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp.Instructions;

internal sealed class UnaryMathInstructionContext : InstructionContext
{
	internal UnaryMathInstructionContext(LLVMValueRef instruction, ModuleContext module) : base(instruction, module)
	{
		Debug.Assert(Operands.Length == 1);
		ResultTypeSignature = module.GetTypeSignature(instruction.TypeOf);
	}
	public LLVMValueRef Operand => Operands[0];
	public CilOpCode CilOpCode => Opcode switch
	{
		LLVMOpcode.LLVMFNeg => CilOpCodes.Neg,
		_ => throw new NotSupportedException(),
	};

	public string Name => Opcode switch
	{
		LLVMOpcode.LLVMFNeg => nameof(NumericHelper.Negate),
		_ => throw new NotSupportedException(),
	};

	public static bool Supported(LLVMOpcode opcode) => opcode is LLVMOpcode.LLVMFNeg;

	public override void AddInstructions(CilInstructionCollection instructions)
	{
		Module.LoadValue(instructions, Operand);
		if (ResultTypeSignature is CorLibTypeSignature)
		{
			instructions.Add(CilOpCode);
		}
		else if (ResultTypeSignature is TypeDefOrRefSignature)
		{
			TypeDefinition type = ResultTypeSignature.Resolve() ?? throw new NullReferenceException(nameof(type));
			string methodName = Name;
			if (Module.InlineArrayTypes.TryGetValue(type, out InlineArrayContext? arrayType))
			{
				MethodDefinition method = Module.InlineArrayNumericHelperType.Methods.First(m => m.Name == methodName);

				arrayType.GetUltimateElementType(out TypeSignature elementType, out _);

				IMethodDescriptor methodDescriptor = method.MakeGenericInstanceMethod(ResultTypeSignature, elementType);

				instructions.Add(CilOpCodes.Call, methodDescriptor);
			}
			else
			{
				MethodDefinition method = Module.NumericHelperType.Methods.First(m => m.Name == methodName);

				IMethodDescriptor methodDescriptor = method.MakeGenericInstanceMethod(ResultTypeSignature);

				instructions.Add(CilOpCodes.Call, methodDescriptor);
			}
		}
		else
		{
			throw new NotSupportedException();
		}
		AddStore(instructions);
	}
}
