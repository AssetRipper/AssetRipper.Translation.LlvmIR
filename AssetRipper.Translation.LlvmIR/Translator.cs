using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
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
			nint namePtr = Marshal.StringToHGlobalAnsi(name);
			LLVMMemoryBufferRef buffer = LLVM.CreateMemoryBufferWithMemoryRange((sbyte*)ptr, (nuint)content.Length, (sbyte*)namePtr, 1);
			try
			{
				LLVMContextRef context = LLVMContextRef.Create();
				try
				{
					LLVMModuleRef module = context.ParseIR(buffer);
					return Translate(module, options ?? new());
				}
				finally
				{
					// https://github.com/dotnet/LLVMSharp/issues/234
					//context.Dispose();
				}
			}
			finally
			{
				// This fails randomly with no real explanation.
				// I'm fairly certain that the IR text data is only referenced (not copied),
				// so the memory leak of not disposing the buffer is probably not a big deal.
				// https://github.com/dotnet/LLVMSharp/issues/234
				//LLVM.DisposeMemoryBuffer(buffer);

				Marshal.FreeHGlobal(namePtr);
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
			List<(LLVMTypeRef, LLVMMetadataRef)> globalVariableTypes = [];

			foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
			{
				LLVMMetadataRef metadata = LibLLVMSharp.GlobalVariableGetGlobalVariableExpression(globalVariableContext.GlobalVariable);
				LLVMMetadataRef type = metadata.Variable.Type;
				if (type.Handle != IntPtr.Zero)
				{
					globalVariableTypes.Add((globalVariableContext.Type, type));
				}
			}

			for (int i = 0; i < globalVariableTypes.Count; i++)
			{
				(LLVMTypeRef type, LLVMMetadataRef metadata) = globalVariableTypes[i];
				metadata = metadata.PassThroughToBaseTypeIfNecessary();
				globalVariableTypes[i] = (type, metadata);
				if (metadata.Handle == IntPtr.Zero)
				{
					globalVariableTypes.RemoveAt(i);
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
						globalVariableTypes.Add((elementType, metadata.BaseType));
					}
				}
				else if (metadata.Kind is LLVMMetadataKind.LLVMDICompositeTypeMetadataKind && type.Kind is LLVMTypeKind.LLVMStructTypeKind)
				{
					if (!AreCompatible(type, metadata, module))
					{
						globalVariableTypes.RemoveAt(i);
						i--;
						continue;
					}

					int index = 0;
					LLVMTypeRef[] fieldTypes = type.GetSubtypes();
					foreach (LLVMMetadataRef member in metadata.Members)
					{
						globalVariableTypes.Add((fieldTypes[index], member));
					}
				}
			}

			IEnumerable<LLVMMetadataRef> allMetadata = module.GetAllMetadata();

			List<LLVMMetadataRef> types = allMetadata.Where(m => m.IsType).ToList();

			List<LLVMMetadataRef> enumTypes = types.Where(m => m.IsEnum && m.Elements.Length > 0).ToList();

			List<EnumContext> enumContexts = enumTypes.Select(m => EnumContext.Create(moduleContext, m)).ToList();
			enumContexts.AssignNames();
			enumContexts.ForEach(e => e.AddNameAttributes(e.Definition));

			List<LLVMMetadataRef> typesWithIdentifiers = types.Where(m => !string.IsNullOrEmpty(m.Identifier)).ToList();
			List<string> identifiers = typesWithIdentifiers.Select(m => m.IdentifierClean).ToList();

			Dictionary<StructContext, List<LLVMMetadataRef>> validMetadata = globalVariableTypes
				.Where(p => p.Item1.Kind is LLVMTypeKind.LLVMStructTypeKind && p.Item2.Kind is LLVMMetadataKind.LLVMDICompositeTypeMetadataKind)
				.Distinct()
				.ToDictionary(p => moduleContext.Structs.Values.First(s => s.Type == p.Item1), p => (List<LLVMMetadataRef>)[p.Item2]);
			foreach (StructContext structContext in moduleContext.Structs.Values)
			{
				if (validMetadata.ContainsKey(structContext))
				{
					continue;
				}

				List<LLVMMetadataRef> list = [];
				validMetadata[structContext] = list;

				ulong size = structContext.Type.GetABISize(module);

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
					if (!AreCompatible(structContext.Type, metadata, size))
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
					if (!AreCompatible(structContext.Type, metadata, size))
					{
						continue;
					}
					list.Add(metadata);
				}
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

	private static bool AreCompatible(LLVMTypeRef type, LLVMMetadataRef metadata, LLVMModuleRef module)
	{
		return AreCompatible(type, metadata, type.GetABISize(module));
	}

	private static bool AreCompatible(LLVMTypeRef type, LLVMMetadataRef metadata, ulong expectedSize)
	{
		return metadata.SizeInBytes == expectedSize && metadata.Members.Count() == type.SubtypesCount;
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
