using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssetRipper.Translation.LlvmIR.Attributes;
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
		if (!hasName.Module.Options.EmitNameAttributes)
		{
			return;
		}

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

	public static void AssignNames<T>(this IEnumerable<T> items) where T : IHasName
	{
		Dictionary<string, List<T>> demangledNames = new();
		foreach (T item in items)
		{
			if (!demangledNames.TryGetValue(item.CleanName, out List<T>? list))
			{
				list = new();
				demangledNames.Add(item.CleanName, list);
			}
			list.Add(item);
		}

		foreach ((string cleanName, List<T> list) in demangledNames)
		{
			if (list.Count == 1)
			{
				list[0].Name = cleanName;
			}
			else if (list.Select(x => x.MangledName).Distinct().Count() != list.Count)
			{
				// There is at least two items with the same mangled name,
				// so we need to use the index when generating unique names.
				for (int i = 0; i < list.Count; i++)
				{
					T item = list[i];
					item.Name = NameGenerator.GenerateName(cleanName, item.MangledName, i);
				}
			}
			else
			{
				foreach (T item in list)
				{
					item.Name = NameGenerator.GenerateName(cleanName, item.MangledName);
				}
			}
		}
	}
}
