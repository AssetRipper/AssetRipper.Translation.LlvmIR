using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.Cpp;

internal static class IntrinsicFunctionImplementer
{
	public static bool TryFillIntrinsicFunction(FunctionContext context)
	{
		MethodDefinition? implementation = GetInjectedIntrinsic(context.Module, context.Name);
		if (implementation == null || implementation.Parameters.Count != context.Definition.Parameters.Count)
		{
			return false;
		}

		context.Definition.IsAssembly = true;

		CilInstructionCollection instructions = context.Definition.CilMethodBody!.Instructions;

		foreach (Parameter parameter in context.Definition.Parameters)
		{
			instructions.Add(CilOpCodes.Ldarg, parameter);
		}

		instructions.Add(CilOpCodes.Call, implementation);

		instructions.Add(CilOpCodes.Ret);

		return true;
	}

	private static MethodDefinition? GetInjectedIntrinsic(ModuleContext context, string name)
	{
		return context.IntrinsicsType.Methods.FirstOrDefault(t => t.Name == name);
	}
}
