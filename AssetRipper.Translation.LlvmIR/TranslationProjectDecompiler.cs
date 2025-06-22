using AsmResolver.DotNet;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace AssetRipper.Translation.LlvmIR;

public class TranslationProjectDecompiler : WholeProjectDecompiler
{
	public TranslationProjectDecompiler() : base(CreateAssemblyResolver())
	{
		Settings.CheckForOverflowUnderflow = true;
	}

	protected override CSharpDecompiler CreateDecompiler(DecompilerTypeSystem ts)
	{
		CSharpDecompiler decompiler = base.CreateDecompiler(ts);
		decompiler.AstTransforms.Add(PartialInjectionTransform.Instance);
		return decompiler;
	}

	private static UniversalAssemblyResolver CreateAssemblyResolver()
	{
		UniversalAssemblyResolver assemblyResolver = new(null, true, ".NETCoreApp,Version=v9.0");
		assemblyResolver.AddSearchDirectory(AppContext.BaseDirectory); // for any NuGet references
		return assemblyResolver;
	}

	public void DecompileProject(ModuleDefinition module, string outputDirectory)
	{
		string file = Path.GetTempFileName();
		try
		{
			using (FileStream fileStream = new(file, FileMode.Open, FileAccess.Write))
			{
				module.Write(fileStream);
			}
			using PEFile moduleFile = new(file);
			DecompileProject(moduleFile, outputDirectory);
		}
		finally
		{
			File.Delete(file);
		}
	}

	private sealed class PartialInjectionTransform : IAstTransform
	{
		public static PartialInjectionTransform Instance { get; } = new();

		public void Run(AstNode rootNode, TransformContext context)
		{
			foreach (TypeDeclaration typeNode in rootNode.DescendantsAndSelf.OfType<TypeDeclaration>())
			{
				if (typeNode.ClassType is not ClassType.Enum)
				{
					typeNode.Modifiers |= Modifiers.Partial;
				}
			}
		}
	}
}
