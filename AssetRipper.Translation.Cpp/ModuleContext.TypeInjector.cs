using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.Cpp;

internal sealed partial class ModuleContext
{
	private sealed class TypeInjector : IReadOnlyDictionary<Type, TypeDefinition>
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

		#region IReadOnlyDictionary
		IEnumerable<Type> IReadOnlyDictionary<Type, TypeDefinition>.Keys => InjectedTypes.Keys;

		IEnumerable<TypeDefinition> IReadOnlyDictionary<Type, TypeDefinition>.Values => InjectedTypes.Values;

		int IReadOnlyCollection<KeyValuePair<Type, TypeDefinition>>.Count => InjectedTypes.Count;

		bool IReadOnlyDictionary<Type, TypeDefinition>.ContainsKey(Type key)
		{
			return InjectedTypes.ContainsKey(key);
		}

		bool IReadOnlyDictionary<Type, TypeDefinition>.TryGetValue(Type key, [MaybeNullWhen(false)] out TypeDefinition value)
		{
			return InjectedTypes.TryGetValue(key, out value);
		}

		IEnumerator<KeyValuePair<Type, TypeDefinition>> IEnumerable<KeyValuePair<Type, TypeDefinition>>.GetEnumerator()
		{
			return InjectedTypes.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return InjectedTypes.GetEnumerator();
		}
		#endregion
	}
}
