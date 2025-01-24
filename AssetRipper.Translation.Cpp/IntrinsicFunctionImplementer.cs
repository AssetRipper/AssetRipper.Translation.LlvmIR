using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;

namespace AssetRipper.Translation.Cpp;

internal static class IntrinsicFunctionImplementer
{
	public static bool TryHandleIntrinsicFunction(FunctionContext context)
	{
		if (context.Instructions.Count != 0)
		{
			return false;
		}

		CilInstructionCollection instructions = context.Definition.CilMethodBody!.Instructions;

		MethodDefinition? implementation = GetInjectedIntrinsic(context.Module, context.Name);
		if (implementation == null || implementation.Parameters.Count != context.Definition.Parameters.Count)
		{
			TypeDefinition declaringType = context.Module.IntrinsicsType.NestedTypes.First(t => t.Name == "Unimplemented");
			context.Definition.DeclaringType!.Methods.Remove(context.Definition);
			declaringType.Methods.Add(context.Definition);

			instructions.Add(CilOpCodes.Ldnull);
			instructions.Add(CilOpCodes.Throw);
		}
		else
		{
			TypeDefinition declaringType = context.Module.IntrinsicsType.NestedTypes.First(t => t.Name == "Implemented");
			context.Definition.DeclaringType!.Methods.Remove(context.Definition);
			declaringType.Methods.Add(context.Definition);

			foreach (Parameter parameter in context.Definition.Parameters)
			{
				instructions.Add(CilOpCodes.Ldarg, parameter);
			}

			instructions.Add(CilOpCodes.Call, implementation);

			instructions.Add(CilOpCodes.Ret);
		}

		return true;
	}

	private static MethodDefinition? GetInjectedIntrinsic(ModuleContext context, string name)
	{
		return context.IntrinsicsType.Methods.FirstOrDefault(t => t.Name == name);
	}
}
