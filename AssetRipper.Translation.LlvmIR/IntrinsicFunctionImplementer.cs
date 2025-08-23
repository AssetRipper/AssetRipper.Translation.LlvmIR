using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AssetRipper.Translation.LlvmIR;

internal static partial class IntrinsicFunctionImplementer
{
	public static bool TryHandleIntrinsicFunction(FunctionContext context)
	{
		if (!context.IsIntrinsic)
		{
			return false;
		}

		CilInstructionCollection instructions = context.Definition.CilMethodBody!.Instructions;

		if (TryGetInjectedIntrinsic(context.Module, context.MangledName, out MethodDefinition? implementation) && implementation.Parameters.Count == context.Definition.Parameters.Count)
		{
			// Set parameter names to match the implementation.
			for (int i = 0; i < context.Definition.Parameters.Count; i++)
			{
				context.Definition.Parameters[i].GetOrCreateDefinition().Name = implementation.Parameters[i].Name;
			}

			MoveToImplementedType(context);

			foreach (Parameter parameter in context.Definition.Parameters)
			{
				instructions.Add(CilOpCodes.Ldarg, parameter);
			}

			instructions.Add(CilOpCodes.Call, implementation);

			instructions.Add(CilOpCodes.Ret);
		}
		else if (TryImplementNumericOperation(context))
		{
			MoveToImplementedType(context);
		}
		else
		{
			MoveToUnimplementedType(context);

			instructions.Add(CilOpCodes.Ldnull);
			instructions.Add(CilOpCodes.Throw);
		}

		context.Definition.IsAggressiveInlining = true;

		return true;
	}

	private static void MoveToImplementedType(FunctionContext context)
	{
		context.DeclaringType.Namespace = context.Module.Options.GetNamespace("Intrinsics.Implemented");
	}

	private static void MoveToUnimplementedType(FunctionContext context)
	{
		context.DeclaringType.Namespace = context.Module.Options.GetNamespace("Intrinsics.Unimplemented");
	}

	private static bool TryGetInjectedIntrinsic(ModuleContext context, string mangledName, [NotNullWhen(true)] out MethodDefinition? result)
	{
		result = context.IntrinsicsType.Methods.FirstOrDefault(m =>
		{
			if (!m.IsPublic || m.GenericParameters.Count != 0)
			{
				return false;
			}

			return m.FindCustomAttributes(context.HelpersNamespace, nameof(MangledNameAttribute))
				.Select(a => a.Signature?.FixedArguments[0].Element?.ToString())
				.Contains(mangledName);
		});
		return result is not null;
	}

	private static bool TryImplementNumericOperation(FunctionContext context)
	{
		if (context.NormalParameters.Length == 0 || context.IsVoidReturn)
		{
			return false;
		}

		if (!TryGetOperationName(context.MangledName, out string? operationName))
		{
			return false;
		}

		TypeSignature returnTypeSignature = context.Definition.Signature!.ReturnType;
		TypeDefinition returnTypeDefinition = returnTypeSignature.Resolve() ?? throw new NullReferenceException(nameof(returnTypeDefinition));

		MethodSpecification? implementation;
		if (context.Module.InlineArrayTypes.TryGetValue(returnTypeDefinition, out InlineArrayContext? arrayType))
		{
			implementation = context.Module.InlineArrayNumericHelperType.Methods
				.FirstOrDefault(m => StringComparer.OrdinalIgnoreCase.Equals(m.Name, operationName) && m.IsPublic)
				?.MakeGenericInstanceMethod(returnTypeSignature, arrayType.UltimateElementType);
		}
		else
		{
			implementation = context.Module.NumericHelperType.Methods
				.FirstOrDefault(m => StringComparer.OrdinalIgnoreCase.Equals(m.Name, operationName) && m.IsPublic)
				?.MakeGenericInstanceMethod(returnTypeSignature);
		}
		if (implementation is null)
		{
			return false;
		}

		CilInstructionCollection instructions = context.Definition.CilMethodBody!.Instructions;

		instructions.Add(CilOpCodes.Ldarg_0);
		if (implementation.Method!.Signature!.GetTotalParameterCount() == 2)
		{
			instructions.Add(CilOpCodes.Ldarg_1);
		}
		instructions.Add(CilOpCodes.Call, implementation);
		instructions.Add(CilOpCodes.Ret);

		return true;
	}

	private static bool TryGetOperationName(string name, [NotNullWhen(true)] out string? operationName)
	{
		if (SimpleOperationRegex.TryMatch(name, out Match? match))
		{
			operationName = match.Groups[1].Value;
			return true;
		}
		operationName = null;
		return false;
	}

	[GeneratedRegex(@"^llvm\.([a-z0-9_]+)\.([a-z0-9_]+)$")]
	private static partial Regex SimpleOperationRegex { get; }
}
