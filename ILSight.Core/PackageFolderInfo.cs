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
