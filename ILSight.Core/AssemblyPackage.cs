namespace ILSight.Core;

/// <summary>
/// A file holding assemblies and other files: a zip file such as a NuGet package, or an app published
/// as a single-file bundle. Its folders and files are listed from the zip's directory or the bundle's
/// manifest; a file's bytes are read out only when asked for, and the package is not kept open.
/// </summary>
public sealed class AssemblyPackage
{
    public required Dictionary<string, PackageFile> Files { get; init; }

    /// <summary>Reads the bytes of a file out of the package.</summary>
    public required Func<PackageFile, byte[]> Read { get; init; }

    /// <summary>The absolute path of the package file.</summary>
    public required AssemblyPath FilePath { get; init; }

    /// <summary>Whether this is an app published as a single-file bundle, not a zip.</summary>
    public required bool IsBundle { get; init; }

    public required PackageFolder Root { get; init; }

    /// <summary>The folders and files of a package as they are listed, sorted into a tree once all are in.</summary>
    public sealed class FileTree
    {
        public required PackageFolder Root { get; init; }
        public required Dictionary<string, PackageFolder> Folders { get; init; }
        public readonly Dictionary<string, PackageFile> Files = new(StringComparer.Ordinal);

    }
}
