using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>What this machine has installed: the running .NET runtime's shared framework directories and
/// the assemblies in them. For a native build, which runs on
/// no shared framework, those of the newest installed runtime. Each is found on first use and kept; one
/// that cannot be found for now is looked for again the next time.</summary>
public sealed class InstalledRuntime
{
    public InstalledRuntime()
    {
        DirectoryPaths = new Lazy<ImmutableArray<string>>(() => Directories.Select(static d => d.Path).ToImmutableArray());
        FilesByName = new Lazy<IReadOnlyDictionary<string, string>>(() => RuntimeFolders.IndexFiles(Directories), LazyThreadSafetyMode.PublicationOnly);
    }

    /// <summary>The shared framework directories, read where the app is composed
    /// (<see cref="RuntimeFolders.FindDirectories"/>).</summary>
    public required ImmutableArray<(string Framework, string Path)> Directories { get; init; }

    internal Lazy<ImmutableArray<string>> DirectoryPaths { get; }

    internal Lazy<IReadOnlyDictionary<string, string>> FilesByName { get; }
}
