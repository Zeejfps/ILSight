// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.BamlDecompiler;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>Decompiles an assembly into a project as ILSpy does, writing its BAML back as XAML
/// pages beside the code-behind, whose generated members stay out of the C# files. Its override is the app's code the
/// library calls back (CD4, CJ11): its own work (whether an entry is BAML, the XAML's name and item type, the file
/// written) runs through the read's <paramref name="callbacks"/>; the library's calls (the base writer, the BAML
/// decompiled, the code-behind looked up, the generated part made) and what it reads of their objects run between,
/// outside the count.</summary>
internal sealed class BamlProjectDecompiler(
    LoadedModule assembly, IAssemblyResolver resolver, DecompilerSettings settings, IDebugInfoProvider? symbols, CallbackFailure callbacks,
    CancellationToken cancellationToken)
    : WholeProjectDecompiler(settings, resolver, null, null, symbols)
{
    private const string PageItem = "Page";
    private const string ApplicationDefinitionItem = "ApplicationDefinition";

    protected override IEnumerable<ProjectItemInfo> WriteResourceToFile(string fileName, string resourceName, Stream entryStream)
    {
        var isBaml = callbacks.Run(fileName, static name => Baml.IsBamlName(name));
        if (!isBaml)
        {
            return base.WriteResourceToFile(fileName, resourceName, entryStream);
        }

        var xaml = WriteXaml(fileName, entryStream);
        return [xaml];
    }

    private ProjectItemInfo WriteXaml(string fileName, Stream baml)
    {
        var typeSystem = new BamlDecompilerTypeSystem(LoadedAssemblies.GetFile(assembly), AssemblyResolver);
        var result = Baml.Decompile(typeSystem, baml, cancellationToken);
        var codeBehind = FindCodeBehind(typeSystem, result);
        if (codeBehind is null)
        {
            return callbacks.Run((Decompiler: this, result, fileName),
                static s => s.Decompiler.SaveXaml(s.result.Xaml, Baml.XamlName(s.fileName), PageItem));
        }

        // Read of the library's objects before the count: the code-behind's name, cleaned up as the base names files,
        // and whether it defines the application.
        var cleanName = CleanUpPath(codeBehind.ReflectionName);
        var isApplication = IsApplicationDefinition(codeBehind, LoadedAssemblies.GetFile(assembly));
        var item = callbacks.Run((Decompiler: this, result, cleanName, isApplication),
            static s => s.Decompiler.SaveXaml(s.result.Xaml, s.cleanName + Baml.XamlExtension, ItemTypeOf(s.isApplication)));
        var generated = GeneratedPart(codeBehind, result);
        item.PartialTypes = [generated];
        return item;
    }

    private static ITypeDefinition? FindCodeBehind(BamlDecompilerTypeSystem typeSystem, BamlDecompilationResult result)
    {
        if (result.TypeName is null)
        {
            return null;
        }

        return typeSystem.MainModule.GetTypeDefinition(result.TypeName.Value.TopLevelTypeName);
    }

    private ProjectItemInfo SaveXaml(System.Xml.Linq.XDocument xaml, string xamlName, string itemType)
    {
        var path = FullFilePath.Of(Path.Combine(TargetDirectory, xamlName));
        Directory.CreateDirectory(path.Folder);
        xaml.Save(path.Text);
        return new ProjectItemInfo(itemType, xamlName);
    }

    private static string ItemTypeOf(bool isApplication)
    {
        if (isApplication)
        {
            return ApplicationDefinitionItem;
        }

        return PageItem;
    }

    private static PartialTypeInfo GeneratedPart(ITypeDefinition codeBehind, BamlDecompilationResult result)
    {
        var part = new PartialTypeInfo(codeBehind);
        foreach (var member in result.GeneratedMembers)
        {
            part.AddDeclaredMember(member);
        }

        return part;
    }
}
