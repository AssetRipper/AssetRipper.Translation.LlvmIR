using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssetRipper.Translation.LlvmIR.Extensions;

namespace AssetRipper.Translation.LlvmIR;

internal interface IHasName
{
	/// <summary>
	/// The name from LLVM.
	/// </summary>
	string MangledName { get; }
	/// <summary>
	/// The demangled name.
	/// </summary>
	string? DemangledName { get; }
	/// <summary>
	/// A clean name that might not be unique.
	/// </summary>
	string CleanName { get; }
	/// <summary>
	/// The unique name used for output.
	/// </summary>
	string Name { get; set; }
	ModuleContext Module { get; }
}
internal static class IHasNameExtensions
{
	public static void AddNameAttributes(this IHasName hasName, IHasCustomAttribute definition)
	{
		if (hasName.MangledName != hasName.Name)
		{
			MethodDefinition constructor = hasName.Module.InjectedTypes[typeof(MangledNameAttribute)].GetMethodByName(".ctor");
			AddAttribute(constructor, hasName.MangledName);
		}

		if (!string.IsNullOrEmpty(hasName.DemangledName) && hasName.DemangledName != hasName.Name)
		{
			MethodDefinition constructor = hasName.Module.InjectedTypes[typeof(DemangledNameAttribute)].GetMethodByName(".ctor");
			AddAttribute(constructor, hasName.DemangledName);
		}

		if (hasName.CleanName != hasName.Name)
		{
			MethodDefinition constructor = hasName.Module.InjectedTypes[typeof(CleanNameAttribute)].GetMethodByName(".ctor");
			AddAttribute(constructor, hasName.CleanName);
		}

		void AddAttribute(MethodDefinition constructor, string name)
		{
			CustomAttributeSignature signature = new();
			signature.FixedArguments.Add(new(hasName.Module.Definition.CorLibTypeFactory.String, name));
			CustomAttribute attribute = new(constructor, signature);
			definition.CustomAttributes.Add(attribute);
		}
	}
}
