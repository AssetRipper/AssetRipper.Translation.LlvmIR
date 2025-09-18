using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class StructContext : IHasName
{
	/// <inheritdoc/>
	public string MangledName => Type.StructName;

	string? IHasName.DemangledName => null;

	/// <inheritdoc/>
	public string CleanName { get; }

	/// <inheritdoc/>
	public string Name
	{
		get => Definition.Name ?? "";
		set => Definition.Name = value;
	}

	string? IHasName.NativeType => null;

	public ModuleContext Module { get; }

	public TypeDefinition Definition { get; }

	public LLVMTypeRef Type { get; }

	private StructContext(ModuleContext module, TypeDefinition definition, LLVMTypeRef type)
	{
		Module = module;
		Definition = definition;
		Type = type;
		CleanName = ExtractCleanName(MangledName, module.Options.RenamedSymbols);
	}

	public static unsafe StructContext Create(ModuleContext module, LLVMTypeRef type)
	{
		TypeDefinition typeDefinition = new(
			module.Options.GetNamespace("Structures"),
			type.StructName,
			TypeAttributes.Public | TypeAttributes.ExplicitLayout,
			module.Definition.DefaultImporter.ImportType(typeof(ValueType)));
		module.Definition.TopLevelTypes.Add(typeDefinition);
		StructContext structContext = new(module, typeDefinition, type);

		LLVMTargetDataRef targetData = LLVM.GetModuleDataLayout(module.Module);
		ulong size = targetData.ABISizeOfType(type);

		LLVMTypeRef[] array = type.GetSubtypes();
		for (int i = 0; i < array.Length; i++)
		{
			ulong offset = targetData.OffsetOfElement(type, (uint)i);

			LLVMTypeRef subType = array[i];
			TypeSignature fieldType = module.GetTypeSignature(subType);
			string fieldName = $"field_{i}";
			FieldDefinition field = new(fieldName, FieldAttributes.Public, fieldType);
			field.FieldOffset = (int)offset;
			typeDefinition.Fields.Add(field);
		}

		typeDefinition.ClassLayout = new ClassLayout(0, (uint)size);

		return structContext;
	}

	public void AddNameAttributes() => this.AddNameAttributes(Definition);

	private static string RemovePrefix(string name, string prefix)
	{
		return name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
	}

	private static string ExtractCleanName(string name, Dictionary<string, string> renamedSymbols)
	{
		if (renamedSymbols.TryGetValue(name, out string? result))
		{
			if (!NameGenerator.IsValidCSharpName(result))
			{
				throw new ArgumentException($"Renamed symbol '{name}' has an invalid name '{result}'.", nameof(renamedSymbols));
			}
			return result;
		}

		name = RemovePrefix(name, "class.");
		name = RemovePrefix(name, "struct.");
		name = RemovePrefix(name, "union.");
		return NameGenerator.CleanName(name, "Struct");
	}

	private string GetDebuggerDisplay()
	{
		return CleanName;
	}
}
