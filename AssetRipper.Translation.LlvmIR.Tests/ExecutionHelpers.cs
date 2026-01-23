using AsmResolver.DotNet;
using System.Reflection;
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

	public static async Task RunTest(ModuleDefinition module, Func<Assembly, Task> testAction)
	{
		AssemblyLoadContext context = CreateLoadContext();
		
		try
		{
			Assembly assembly = LoadAssembly(context, module);
			await testAction.Invoke(assembly);
		}
		finally
		{
			context.Unload();

			GC.Collect();
		}
	}

	public static MethodInfo GetMethod(Assembly assembly, string name)
	{
		Type type = assembly.GetType("GlobalMembers") ?? throw new NullReferenceException(nameof(type));
		MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static) ?? throw new NullReferenceException(nameof(method));
		return method;
	}

	public static T GetMethod<T>(Assembly assembly, string name) where T : Delegate
	{
		MethodInfo method = GetMethod(assembly, name);
		return method.CreateDelegate<T>();
	}
}
