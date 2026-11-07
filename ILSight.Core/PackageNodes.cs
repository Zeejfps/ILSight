using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>A folder of a listed package, labelled <see cref="Label"/>: the package's root, labelled with
/// its file's name, or a folder in it, which when it holds nothing but one folder is merged with it into
/// one, labelled with both names, <c>lib/net8.0</c>, and listing the deeper one's contents.</summary>
public sealed class PackageFolderInfo
{
    public required LoadedPackage Package { get; init; }

    /// <summary>The first of the folders merged into one, the folder itself when none are.</summary>
    public required PackageFolder Outermost { get; init; }

    /// <summary>The folder whose contents it lists.</summary>
    public required PackageFolder Folder { get; init; }

    public required string Label { get; init; }

    /// <summary>Whether this is the package's root, standing for the package itself.</summary>
    public bool IsRoot => ReferenceEquals(Folder, Package.Package.Root);

    public string FullName => IsRoot ? Package.FilePath.Text : Folder.FullName;
}

internal static class PackageFolders
{
    /// <summary>Whether the folder at <paramref name="path"/> is the one at <paramref name="folder"/> or in it.</summary>
    public static bool IsWithin(string path, string folder)
    {
        if (path == folder)
            return true;
        return path.StartsWith(folder + "/", StringComparison.Ordinal);
    }

    /// <summary>The path of the folder holding the entry at <paramref name="path"/>; empty at the root.</summary>
    public static string FolderOf(string path)
    {
        var at = path.LastIndexOf('/');
        if (at < 0)
            return "";
        return path.Substring(0, at);
    }

    /// <summary>Whether the folder at <paramref name="path"/>, from the package's root, is one of those
    /// merged into this one.</summary>
    internal static bool ShowsFolder(PackageFolderInfo info, string path)
    {
        if (!PackageFolders.IsWithin(info.Folder.FullName, path))
            return false;
        return PackageFolders.IsWithin(path, info.Outermost.FullName);
    }

    internal static PackageFolderInfo CreateRootFolder(LoadedPackage package)
    {
        return new PackageFolderInfo { Package = package, Outermost = package.Package.Root, Folder = package.Package.Root, Label = package.FilePath.FileName };
    }
}

/// <summary>A file of a package that is no assembly: shown as text, a picture or its bytes, read out of
/// the package when needed.</summary>
public sealed class PackageFileInfo
{
    public required LoadedPackage Package { get; init; }

    public required PackageFile File { get; init; }

    /// <summary>Whether it is shown as a picture, being named as one is.</summary>
    public bool IsImage => ResourceImages.IsImageName(File.Name);

    public string Description => $"{File.FullName} ({File.Length} bytes)";

    public string FileName => File.Name;
}

internal static class PackageContents
{
    private static readonly string[] AssemblyExtensions = [".dll", ".exe", ".winmd"];

    public static bool IsAssemblyName(string name)
    {
        foreach (var extension in AssemblyExtensions)
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static byte[] ReadFileData(PackageFileInfo info)
    {
        return AssemblyPackages.ReadFile(info.Package.Package, info.File);
    }
}
