using AsmResolver.DotNet;
using NUnit.Framework;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace AssetRipper.Translation.Cpp.Tests;

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

	public static void RunTest(ModuleDefinition module, Action<Assembly> testAction)
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

				Type? type = assembly.GetType("PointerCache");
				Assert.That(type, Is.Not.Null);

				foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
				{
					Assert.That(field.FieldType, Is.EqualTo(typeof(IntPtr)));
					IntPtr pointer = (IntPtr)field.GetValue(null)!;
					if (pointer != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(pointer);
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
