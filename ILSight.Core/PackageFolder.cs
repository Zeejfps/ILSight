namespace ILSight.Core;

/// <summary>A folder in a <see cref="AssemblyPackage"/>, with the folders and files right inside
/// it, each sorted by name.</summary>
public sealed class PackageFolder
{
    public required string Name { get; init; }

    /// <summary>The path from the package's root, with <c>/</c> between folders; empty for the root.</summary>
    public required string FullName { get; init; }

    public IReadOnlyList<PackageFolder> Folders { get; init; } = [];

    public IReadOnlyList<PackageFile> Files { get; init; } = [];
}
