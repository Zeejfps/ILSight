using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>What this machine has installed: the running .NET runtime's shared framework directories and
/// the assemblies in them. For a native build, which runs on no shared framework, those of the newest installed
/// runtime. Both are read where the app is composed (<see cref="RuntimeFolders"/>).</summary>
public sealed class InstalledRuntime
{
    public InstalledRuntime()
    {
        DirectoryPaths = new Lazy<ImmutableArray<string>>(() => Directories.Select(static d => d.Path).ToImmutableArray());
    }

    /// <summary>The shared framework directories (<see cref="RuntimeFolders.FindDirectories"/>).</summary>
    public required ImmutableArray<(string Framework, string Path)> Directories { get; init; }

    /// <summary>The assembly files in <see cref="Directories"/>, by simple name, the first directory's first
    /// (<see cref="RuntimeFolders.IndexFiles"/>).</summary>
    public required IReadOnlyDictionary<string, string> FilesByName { get; init; }

    internal Lazy<ImmutableArray<string>> DirectoryPaths { get; }
}
