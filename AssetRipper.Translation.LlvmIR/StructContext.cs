using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AssetRipper.Translation.LlvmIR;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed partial class StructContext : IHasName
{
	/// <inheritdoc/>
	public string MangledName => Type.StructName;

	/// <inheritdoc/>
	public string? DemangledName { get; }

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
		DemangledName = ExtractDemangledName(MangledName);
		CleanName = ExtractCleanName(MangledName, DemangledName, module.Options.RenamedSymbols);
	}

	public static unsafe StructContext Create(ModuleContext module, LLVMTypeRef type)
	{
		TypeDefinition typeDefinition = new(
			module.Options.GetNamespace("Structures"),
			$"{type.StructName}_{Guid.NewGuid()}",
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

	private static string ExtractCleanName(string mangledName, string demangledName, Dictionary<string, string> renamedSymbols)
	{
		if (renamedSymbols.TryGetValue(mangledName, out string? result))
		{
			if (!NameGenerator.IsValidCSharpName(result))
			{
				throw new ArgumentException($"Renamed symbol '{mangledName}' has an invalid name '{result}'.", nameof(renamedSymbols));
			}
			return result;
		}
		else
		{
			return NameGenerator.CleanName(demangledName, "Struct");
		}
	}

	private static string ExtractDemangledName(string name)
	{
		name = name.RemovePrefix("class.").RemovePrefix("struct.").RemovePrefix("union.");

		Match match = NumericalSuffix.Match(name);
		if (match.Success)
		{
			return match.Groups[1].Value;
		}
		else
		{
			return name;
		}
	}

	private string GetDebuggerDisplay()
	{
		return CleanName;
	}

	[GeneratedRegex(@"^(.*)\.\d+$")]
	private static partial Regex NumericalSuffix { get; }
}
