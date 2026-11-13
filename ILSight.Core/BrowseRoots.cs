using System.Diagnostics;

namespace ILSight.Core;

/// <summary>The root of a listed item's tree (a module not read out of a package, a package, a file that could not be
/// read), made from the item's values alone: its name, path, version and, for a package, its root folder. Reads no
/// file; a module read out of a package sits under its package's folder, which <see cref="BrowseSessions"/> builds.</summary>
internal static class BrowseRoots
{
    private static readonly Activation.OpenSelf Open = new();

    /// <summary>The root of the listed <paramref name="item"/>'s tree.</summary>
    internal static BrowseNode Create(LoadedAssembly item)
    {
        return item switch
        {
            LoadedModule module => Create(module, new NodeSubject.Module { Value = module }, GetModuleLabel(module), module.FullName, module.Name, canHaveChildren: true, isCode: true,
                ilspyName: module.FilePath.Text),
            LoadedPackage package => CreatePackageRoot(package),
            FailedAssembly failed => Create(failed, new NodeSubject.FailedAssembly { Value = failed }, failed.FilePath.FileName, failed.FilePath.Text, failed.FilePath.FileName, canHaveChildren: false, isCode: false,
                ilspyName: null),
            _ => throw new UnreachableException(),
        };
    }

    private static BrowseNode CreatePackageRoot(LoadedPackage package)
    {
        var folder = PackageFolders.CreateRootFolder(package);
        return Create(package, new NodeSubject.Package { Value = folder }, folder.Label, folder.FullName, folder.Label, canHaveChildren: true, isCode: true, ilspyName: null);
    }

    // ilspyName is the name ILSpy's tree gives the root: a module's file path; none for a package or a failed file.
    private static BrowseNode Create(LoadedAssembly item, NodeSubject subject, string text, string fullName, string name, bool canHaveChildren, bool isCode,
        string? ilspyName)
    {
        return new BrowseNode
        {
            Assembly = item,
            Parent = null,
            Text = text,
            Subject = subject,
            EnclosingNamespace = null,
            Segment = null,
            Path = new NodePath { FilePath = item.FilePath, Steps = [] },
            FullName = fullName,
            Visibility = NodeVisibility.Open,
            Reference = null,
            Activation = Open,
            Analysis = null,
            CanHaveChildren = canHaveChildren,
            Name = name,
            IsCode = isCode,
            ResourceFileName = null,
            ILSpyName = ilspyName,
            EntityFullName = null,
        };
    }

    /// <summary>The module's name, version and the framework it was built for. The framework picked to
    /// resolve its references against is left out, so that the label, by which paths find the node, stays
    /// the same when another is picked.</summary>
    internal static string GetModuleLabel(LoadedModule module)
    {
        var details = new[] { module.Version?.ToString(), FindBuiltForName(module) }.OfType<string>().ToArray();
        return details.Length == 0 ? module.Name : $"{module.Name} ({string.Join(", ", details)})";
    }

    private static string? FindBuiltForName(LoadedModule module)
    {
        if (module.DetectedTargetFramework is null)
        {
            return null;
        }

        return module.DetectedTargetFramework.Readable;
    }
}
