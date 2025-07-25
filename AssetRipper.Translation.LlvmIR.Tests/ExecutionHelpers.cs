using AsmResolver.DotNet;
using NUnit.Framework;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace AssetRipper.Translation.LlvmIR.Tests;

internal static class ExecutionHelpers
{
	private static AssemblyLoadContext CreateLoadContext()
	{
		return new AssemblyLoadContext(null, true);
	}

	private static Assembly LoadAssembly(AssemblyLoadContext context, ModuleDefinition module)
	{
		using MemoryStream stream = new();
		module.Write(stream);
		stream.Position = 0; // Reset stream position to the beginning
		return context.LoadFromStream(stream);
	}

	public static unsafe void RunTest(ModuleDefinition module, Action<Assembly> testAction)
	{
		AssemblyLoadContext context = CreateLoadContext();
		
		try
		{
			Assembly assembly = LoadAssembly(context, module);

			try
			{
				testAction.Invoke(assembly);
			}
			finally
			{
				// Free unmanaged resources

				foreach (Type type in assembly.GetTypes().Where(t => t.Namespace == "GlobalVariables"))
				{
					foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
					{
						Pointer pointer = (Pointer)field.GetValue(null)!;
						IntPtr value = (IntPtr)Pointer.Unbox(pointer);
						if (value != IntPtr.Zero)
						{
							Marshal.FreeHGlobal(value);
						}
					}
				}
			}
		}
		finally
		{
			context.Unload();

			GC.Collect();
		}
	}
}
