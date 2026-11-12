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
}
