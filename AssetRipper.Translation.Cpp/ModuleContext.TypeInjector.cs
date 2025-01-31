using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using System.Diagnostics;

namespace AssetRipper.Translation.Cpp;

internal sealed partial class ModuleContext
{
	private sealed class TypeInjector
	{
		public TypeInjector(ModuleDefinition targetModule, string? targetNamespace = null)
		{
			TargetModule = targetModule;
			TargetNamespace = targetNamespace;
		}
		private ModuleDefinition TargetModule { get; }
		private string? TargetNamespace { get; }
		private Dictionary<string, ModuleDefinition> SourceModules { get; } = new();
		private Dictionary<Type, TypeDefinition> InjectedTypes { get; } = new();

		public TypeDefinition this[Type type] => InjectedTypes[type];

		public TypeInjector Inject(params ReadOnlySpan<Type> types)
		{
			MemberCloner cloner = new(TargetModule);
			foreach (Type type in types)
			{
				ModuleDefinition sourceModule = GetOrLoadSourceModule(type);
				cloner.Include(sourceModule.TopLevelTypes.First(t => t.Namespace == type.Namespace && t.Name == type.Name));
			}
			ICollection<TypeDefinition> clonedTypes = cloner.Clone().ClonedTopLevelTypes;
			Debug.Assert(clonedTypes.Count == types.Length);
			foreach (Type type in types)
			{
				TypeDefinition clonedType = clonedTypes.First(t => t.Namespace == type.Namespace && t.Name == type.Name);
				clonedType.Namespace = TargetNamespace;
				InjectedTypes.Add(type, clonedType);
				TargetModule.TopLevelTypes.Add(clonedType);
			}

			return this;
		}

		private ModuleDefinition GetOrLoadSourceModule(Type type)
		{
			string assemblyPath = type.Assembly.Location;
			if (!SourceModules.TryGetValue(assemblyPath, out ModuleDefinition? module))
			{
				module = ModuleDefinition.FromFile(assemblyPath);
				SourceModules.Add(assemblyPath, module);
			}
			return module;
		}
	}
}
