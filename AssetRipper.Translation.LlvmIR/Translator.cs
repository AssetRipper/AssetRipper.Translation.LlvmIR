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

		moduleContext.IdentifyFunctionsThatMightThrow();

		foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
		{
			globalVariableContext.CreateProperties();
		}

		foreach (GlobalVariableContext globalVariableContext in moduleContext.GlobalVariables.Values)
		{
			globalVariableContext.InitializeData();
			globalVariableContext.AddPublicImplementation();
		}

		foreach (FunctionContext functionContext in moduleContext.Methods.Values)
		{
			functionContext.AddNameAttributes(functionContext.DeclaringType);
			functionContext.AddPublicImplementation();

			if (IntrinsicFunctionImplementer.TryHandleIntrinsicFunction(functionContext))
			{
				continue;
			}

			CilInstructionCollection instructions = functionContext.Definition.CilMethodBody!.Instructions;

			IReadOnlyList<BasicBlock> basicBlocks = InstructionLifter.Lift(functionContext);
			InstructionOptimizer.Optimize(basicBlocks);

			if (functionContext.NeedsStackFrame)
			{
				Debug.Assert(functionContext.LocalVariablesType is not null);
				Debug.Assert(functionContext.StackFrameVariable is not null);
				TypeDefinition stackFrameListType = moduleContext.InjectedTypes[typeof(StackFrameList)];

				instructions.Add(CilOpCodes.Ldsflda, stackFrameListType.GetFieldByName(nameof(StackFrameList.Current)));
				instructions.Add(CilOpCodes.Call, stackFrameListType.GetMethodByName(nameof(StackFrameList.New)).MakeGenericInstanceMethod(functionContext.LocalVariablesType.ToTypeSignature()));
				instructions.Add(CilOpCodes.Stloc, functionContext.StackFrameVariable);
			}

			foreach (BasicBlock basicBlock in basicBlocks)
			{
				basicBlock.AddInstructions(instructions);
			}

			instructions.OptimizeMacros();
		}

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

		// Structs are discovered dynamically, so we need to assign names after all methods are created.
		moduleContext.AssignStructNames();

		return moduleDefinition;
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
