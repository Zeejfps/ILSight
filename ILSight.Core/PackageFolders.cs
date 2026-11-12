namespace ILSight.Core;

internal static class PackageFolders
{
    /// <summary>Whether the folder at <paramref name="path"/> is the one at <paramref name="folder"/> or in it.</summary>
    public static bool IsWithin(string path, string folder)
    {
        if (path == folder)
        {
            return true;
        }

        return path.StartsWith(folder + "/", StringComparison.Ordinal);
    }

    /// <summary>The path of the folder holding the entry at <paramref name="path"/>; empty at the root.</summary>
    public static string FolderOf(string path)
    {
        var at = path.LastIndexOf('/');
        if (at < 0)
        {
            return "";
        }

        return path.Substring(0, at);
    }

    /// <summary>Whether the folder at <paramref name="path"/>, from the package's root, is one of those
    /// merged into this one.</summary>
    internal static bool ShowsFolder(PackageFolderInfo info, string path)
    {
        if (!PackageFolders.IsWithin(info.Folder.FullName, path))
        {
            return false;
        }

        return PackageFolders.IsWithin(path, info.Outermost.FullName);
    }

    internal static PackageFolderInfo CreateRootFolder(LoadedPackage package)
    {
        return new PackageFolderInfo { Package = package, Outermost = package.Package.Root, Folder = package.Package.Root, Label = package.FilePath.FileName };
    }
}
