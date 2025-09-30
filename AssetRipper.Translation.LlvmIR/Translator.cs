using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AssetRipper.Translation.LlvmIR.Attributes;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Instructions;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AssetRipper.Translation.LlvmIR;

public static unsafe class Translator
{
	static Translator()
	{
		Patches.Apply();
	}

	public static ModuleDefinition Translate(string name, string content, TranslatorOptions? options = null)
	{
		return Translate(name, Encoding.UTF8.GetBytes(content), options);
	}

	public static ModuleDefinition Translate(string name, ReadOnlySpan<byte> content, TranslatorOptions? options = null)
	{
		fixed (byte* ptr = content)
		{
			using LLVMContextRef context = LLVMContextRef.Create();
			nint namePtr = Marshal.StringToHGlobalAnsi(name);
			LLVMMemoryBufferRef buffer = LLVM.CreateMemoryBufferWithMemoryRange((sbyte*)ptr, (nuint)content.Length, (sbyte*)namePtr, 0);
			try
			{
				using LLVMModuleRef module = context.ParseIR(buffer);
				return Translate(module, options ?? new());
			}
			finally
			{
				// This fails randomly with no real explanation.
				// The IR text data is only referenced (not copied),
				// so the memory leak of not disposing the buffer is negligible.
				//LLVM.DisposeMemoryBuffer(buffer);

				Marshal.FreeHGlobal(namePtr);

				// Collect any memory that got allocated.
				GC.Collect();
			}
		}
	}

	private static ModuleDefinition Translate(LLVMModuleRef module, TranslatorOptions options)
	{
		CustomModuleDefinition moduleDefinition = new(string.IsNullOrEmpty(options.ModuleName) ? "ConvertedCpp" : options.ModuleName);

		moduleDefinition.AddTargetFrameworkAttributeForDotNet9();

		ModuleContext moduleContext = new(module, moduleDefinition, options);

		foreach (LLVMValueRef global in module.GetGlobals())
		{
			GlobalVariableContext globalVariableContext = new(global, moduleContext);
			moduleContext.GlobalVariables.Add(global, globalVariableContext);
		}

		moduleContext.CreateFunctions();

		moduleContext.AssignMemberNames();

		Console.WriteLine("Identifying functions that might throw exceptions...");
		moduleContext.IdentifyFunctionsThatMightThrow();

		Console.WriteLine("Creating properties for global variables...");
		foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
		{
			globalVariableContext.CreateProperties();
		}

		Console.WriteLine("Initializing data for global variables");
		foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
		{
			globalVariableContext.InitializeData();
			globalVariableContext.AddPublicImplementation();
		}

		Console.WriteLine("Implementing functions...");
		int functionIndex = 1;
		foreach (FunctionContext functionContext in moduleContext.Methods.Values)
		{
			functionContext.AddNameAttributes(functionContext.DeclaringType);
			functionContext.AddTypeAttribute(functionContext.Definition);
			functionContext.AddPublicImplementation();

			if (IntrinsicFunctionImplementer.TryHandleIntrinsicFunction(functionContext))
			{
				continue;
			}

			if (functionIndex % 100 == 0)
			{
				Console.WriteLine($"Implementing function {functionIndex}/{moduleContext.Methods.Count}");
			}

			CilInstructionCollection instructions = functionContext.Definition.CilMethodBody!.Instructions;

			IReadOnlyList<BasicBlock> basicBlocks = InstructionLifter.Lift(functionContext);
			InstructionOptimizer.Optimize(basicBlocks);

			foreach (BasicBlock basicBlock in basicBlocks)
			{
				basicBlock.AddInstructions(instructions);
			}

			instructions.OptimizeMacros();

			functionIndex++;
		}

		Console.WriteLine("Cleaning up...");
		foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
		{
			globalVariableContext.RemovePointerFieldIfNotUsed();
		}

		foreach (FunctionContext functionContext in moduleContext.Methods.Values)
		{
			functionContext.RemovePointerFieldIfNotUsed();
		}

		if (moduleContext.InjectedTypes[typeof(AssemblyFunctions)].Methods.Count == 0)
		{
			moduleDefinition.TopLevelTypes.Remove(moduleContext.InjectedTypes[typeof(AssemblyFunctions)]);
			moduleDefinition.TopLevelTypes.Remove(moduleContext.InjectedTypes[typeof(InlineAssemblyAttribute)]);
		}

		{
			List<LLVMMetadataRef> types = module.GetAllMetadata().Where(m => m.IsType).ToList();

			CreateEnumerations(moduleContext, types);

			List<(LLVMTypeRef, LLVMMetadataRef)> globalVariableTypes = [];

			foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
			{
				LLVMMetadataRef metadata = LibLLVMSharp.GlobalVariableGetGlobalVariableExpression(globalVariableContext.GlobalVariable);
				LLVMMetadataRef type = metadata.Variable.Type;
				if (type.Handle == IntPtr.Zero)
				{
				}
				else if (type.IsArray && globalVariableContext.Type.Kind is LLVMTypeKind.LLVMArrayTypeKind or LLVMTypeKind.LLVMScalableVectorTypeKind or LLVMTypeKind.LLVMVectorTypeKind)
				{
					globalVariableTypes.Add((globalVariableContext.Type, type));
				}
				else if ((type.IsStruct || type.IsClass || type.IsUnion) && globalVariableContext.Type.Kind is LLVMTypeKind.LLVMStructTypeKind)
				{
					globalVariableTypes.Add((globalVariableContext.Type, type));
				}
			}

			AddChildTypes(globalVariableTypes);

			List<LLVMMetadataRef> typesWithIdentifiers = types.Where(m => m.IsStruct || m.IsClass || m.IsUnion).ToList();
			List<string> identifiers = typesWithIdentifiers.Select(m =>
			{
				string identifier = m.IdentifierClean;
				return string.IsNullOrEmpty(identifier) ? m.Name : identifier;
			}).ToList();

			Dictionary<LLVMTypeRef, StructContext> contextLookUp = moduleContext.Structs.Values.ToDictionary(s => s.Type);

			Dictionary<StructContext, List<LLVMMetadataRef>> validMetadata = globalVariableTypes
				.Where(p => p.Item1.Kind is LLVMTypeKind.LLVMStructTypeKind && p.Item2.Kind is LLVMMetadataKind.LLVMDICompositeTypeMetadataKind)
				.Distinct()
				.ToDictionary(p => contextLookUp[p.Item1], p => (List<LLVMMetadataRef>)[p.Item2]);
			foreach (StructContext structContext in moduleContext.Structs.Values)
			{
				if (validMetadata.ContainsKey(structContext))
				{
					continue;
				}

				List<LLVMMetadataRef> list = [];
				validMetadata[structContext] = list;

				if (string.IsNullOrEmpty(structContext.DemangledName))
				{
					continue;
				}

				for (int i = 0; i < identifiers.Count; i++)
				{
					if (structContext.DemangledName != identifiers[i])
					{
						continue;
					}
					LLVMMetadataRef metadata = typesWithIdentifiers[i];
					if (!AreCompatible(structContext.Type, metadata))
					{
						continue;
					}
					list.Add(metadata);
				}

				if (list.Count > 0)
				{
					continue;
				}

				for (int i = 0; i < identifiers.Count; i++)
				{
					string identifier = identifiers[i];
					if (identifier.Length <= structContext.DemangledName.Length || !identifier.StartsWith(structContext.DemangledName, StringComparison.Ordinal) || identifier[structContext.DemangledName.Length] != '<')
					{
						continue;
					}

					bool restIsTemplate = true;
					int angleBracketDepth = 0;
					for (int j = structContext.DemangledName.Length + 1; j < identifier.Length; j++)
					{
						char c = identifier[j];
						if (c == '<')
						{
							angleBracketDepth++;
						}
						else if (c == '>')
						{
							angleBracketDepth--;
							if (angleBracketDepth < 0)
							{
								restIsTemplate = j == identifier.Length - 1;
								break;
							}
						}
					}

					if (!restIsTemplate)
					{
						continue;
					}
					LLVMMetadataRef metadata = typesWithIdentifiers[i];
					if (!AreCompatible(structContext.Type, metadata))
					{
						continue;
					}
					list.Add(metadata);
				}
			}

			List<(LLVMTypeRef, LLVMMetadataRef)> types2 = validMetadata.Where(p => p.Value.Count > 0).Select(p => (p.Key.Type, p.Value[0])).ToList();
			AddChildTypes(types2);

			foreach ((LLVMTypeRef type, LLVMMetadataRef metadata) in types2)
			{
				if (metadata.Handle == IntPtr.Zero)
				{
					continue;
				}
				if (!(metadata.IsStruct || metadata.IsClass || metadata.IsUnion))
				{
					continue;
				}
				if (type.Kind is not LLVMTypeKind.LLVMStructTypeKind)
				{
					continue;
				}
				if (!contextLookUp.TryGetValue(type, out StructContext? structContext))
				{
					continue;
				}
				List<LLVMMetadataRef> list = validMetadata[structContext];
				list.Clear();
				list.Add(metadata);
			}

			foreach ((StructContext structContext, List<LLVMMetadataRef> list) in validMetadata)
			{
				if (list.Count is 0)
				{
					continue;
				}

				LLVMMetadataRef[] members = list[0].Members.ToArray();
				string[] memberNames = members.Select(m => m.Name).ToArray();
				FieldDefinition[] fields = structContext.Definition.Fields.Where(f => !f.IsStatic).ToArray();
				Debug.Assert(members.Length == fields.Length);

				bool allMatch = true;
				for (int index = 1; index < list.Count; index++)
				{
					string[] otherMemberNames = list[index].Members.Select(m => m.Name).ToArray();
					allMatch &= memberNames.AsSpan().SequenceEqual(otherMemberNames);
					if (!allMatch)
					{
						break;
					}
				}
				if (!allMatch)
				{
					continue;
				}

				FieldDefinitionHasName[] fieldDefinitions = new FieldDefinitionHasName[fields.Length];
				for (int i = 0; i < fields.Length; i++)
				{
					fieldDefinitions[i] = new(fields[i], memberNames[i], i, moduleContext);
				}

				fieldDefinitions.AssignNames();
			}
		}

		// Structs and inline arrays are discovered dynamically, so we need to assign names after all methods are created.
		moduleContext.AssignStructNames();
		moduleContext.AssignInlineArrayNames();

		return moduleDefinition;
	}

	private static void AddChildTypes(List<(LLVMTypeRef, LLVMMetadataRef)> list)
	{
		for (int i = 0; i < list.Count; i++)
		{
			(LLVMTypeRef type, LLVMMetadataRef metadata) = list[i];
			metadata = metadata.PassThroughToBaseTypeIfNecessary();
			list[i] = (type, metadata);
			if (metadata.Handle == IntPtr.Zero)
			{
				list.RemoveAt(i);
				i--;
				continue;
			}

			if (metadata.IsArray && type.Kind is LLVMTypeKind.LLVMArrayTypeKind or LLVMTypeKind.LLVMScalableVectorTypeKind or LLVMTypeKind.LLVMVectorTypeKind)
			{
				uint arrayLength;
				LLVMTypeRef elementType;
				if (type.Kind == LLVMTypeKind.LLVMArrayTypeKind)
				{
					arrayLength = type.ArrayLength;
					elementType = type.ElementType;
				}
				else
				{
					// https://github.com/dotnet/LLVMSharp/pull/235
					arrayLength = LLVM.GetVectorSize(type);
					elementType = LLVM.GetElementType(type);
				}

				if (arrayLength == metadata.ArrayLength)
				{
					list.Add((elementType, metadata.BaseType));
				}
			}
			else if ((metadata.IsStruct || metadata.IsClass || metadata.IsUnion) && type.Kind is LLVMTypeKind.LLVMStructTypeKind)
			{
				if (!AreCompatible(type, metadata))
				{
					list.RemoveAt(i);
					i--;
					continue;
				}

				int index = 0;
				LLVMTypeRef[] fieldTypes = type.GetSubtypes();
				foreach (LLVMMetadataRef member in metadata.Members)
				{
					list.Add((fieldTypes[index], member.BaseType));
				}
			}
		}
	}

	private static void CreateEnumerations(ModuleContext moduleContext, List<LLVMMetadataRef> types)
	{
		List<LLVMMetadataRef> enumTypes = types.Where(m => m.IsEnum && m.Elements.Length > 0).ToList();

		List<EnumContext> enumContexts = enumTypes.Select(m => EnumContext.Create(moduleContext, m)).ToList();
		enumContexts.AssignNames();
		enumContexts.ForEach(e => e.AddNameAttributes(e.Definition));
	}

	private static bool AreCompatible(LLVMTypeRef type, LLVMMetadataRef metadata)
	{
		return metadata.Members.Count() == type.SubtypesCount;
	}

	private sealed class FieldDefinitionHasName(FieldDefinition field, string debugName, int index, ModuleContext module) : IHasName
	{
		public string MangledName => $"{debugName}_{index}";
		string? IHasName.DemangledName => null;
		public string CleanName { get; } = NameGenerator.CleanName(debugName, "field");
		public string Name { get => @field.Name ?? ""; set => @field.Name = value; }
		string? IHasName.NativeType => null;
		ModuleContext IHasName.Module => module;
	}

	private sealed class CustomModuleDefinition(string name) : ModuleDefinition(name, KnownCorLibs.SystemRuntime_v9_0_0_0)
	{
		protected override ReferenceImporter GetDefaultImporter()
		{
			return new CustomReferenceImporter(this);
		}
	}

	private sealed class CustomReferenceImporter(CustomModuleDefinition module) : ReferenceImporter(module)
	{
		protected override AssemblyReference ImportAssembly(AssemblyDescriptor assembly)
		{
			// This importer will fail if System.Runtime.InteropServices.Marshal is ever imported.
			// At runtime, Marshal is part of System.Private.CoreLib.
			// However, at compile time, it is not part of System.Runtime, but rather System.Runtime.InteropServices.
			// If we ever try to import it, the reference will be invalid.
			// This is one of the primary reasons for NativeMemoryHelper, which allows us to avoid referencing Marshal directly.
			if (SignatureComparer.Default.Equals(assembly, KnownCorLibs.SystemPrivateCoreLib_v9_0_0_0))
			{
				return base.ImportAssembly(KnownCorLibs.SystemRuntime_v9_0_0_0);
			}
			else
			{
				return base.ImportAssembly(assembly);
			}
		}
	}
}
