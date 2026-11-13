namespace ILSight.Core;

internal static class PackageNodes
{
    /// <summary>The folder's folders, then its files. An assembly file is the root of that assembly's tree,
    /// read out of the package now, so that it shows as the assembly it is; any other file is shown as a
    /// resource is.</summary>
    internal static BrowseNode[] ListPackageFolder(BrowseNode parent, PackageFolderInfo folder)
    {
        var package = folder.Package;
        var children = new List<BrowseNode>();
        foreach (var child in folder.Folder.Folders)
        {
            children.Add(BrowseNodes.Create(parent, new NodeSubject.Package { Value = CreateMergedFolder(package, child) }));
        }

        foreach (var file in folder.Folder.Files)
        {
            children.Add(CreateFileNode(parent, package, file));
        }

        return children.ToArray();
    }

    private static PackageFolderInfo CreateMergedFolder(LoadedPackage package, PackageFolder folder)
    {
        var shown = folder;
        var label = folder.Name;
        while (HoldsOnlyOneFolder(shown))
        {
            shown = shown.Folders[0];
            label = $"{label}/{shown.Name}";
        }
        return new PackageFolderInfo { Package = package, Outermost = folder, Folder = shown, Label = label };
    }

    private static bool HoldsOnlyOneFolder(PackageFolder folder)
    {
        if (folder.Files.Count != 0)
        {
            return false;
        }

        return folder.Folders.Count == 1;
    }

    private static BrowseNode CreateFileNode(BrowseNode parent, LoadedPackage package, PackageFile file)
    {
        var assembly = PackageContents.IsAssemblyName(file.Name) ? LoadedAssemblies.FindAssembly(package.Package, LoadedAssemblies.EnsureAssemblies(package), file) : null;
        if (assembly is null)
        {
            return BrowseNodes.Create(parent, new NodeSubject.PackageFile { Value = new PackageFileInfo { Package = package, File = file } });
        }

        return ModuleNodes.CreateModuleRoot(assembly, parent);
    }
}
