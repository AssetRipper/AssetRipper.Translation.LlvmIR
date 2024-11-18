using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
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

	public static TypeDefinition InjectIntrinsics(ModuleDefinition targetModule)
	{
		ModuleDefinition executingModule = ModuleDefinition.FromFile(typeof(ModuleContext).Assembly.Location);
		MemberCloner cloner = new(targetModule);
		cloner.Include(executingModule.TopLevelTypes.First(t => t.Name == nameof(IntrinsicFunctions)));
		TypeDefinition result = cloner.Clone().ClonedTopLevelTypes.Single();
		result.Namespace = null;
		targetModule.TopLevelTypes.Add(result);
		return result;
	}
}
