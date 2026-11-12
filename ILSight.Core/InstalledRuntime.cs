using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>What this machine has installed: the running .NET runtime's shared framework directories and
/// the assemblies in them, and the ready-made assembly lists it offers. For a native build, which runs on
/// no shared framework, those of the newest installed runtime. Each is found on first use and kept; one
/// that cannot be found for now is looked for again the next time.</summary>
public sealed class InstalledRuntime
{
    public InstalledRuntime()
    {
        Directories = new Lazy<ImmutableArray<(string Framework, string Path)>>(RuntimeAssemblies.FindDirectories, LazyThreadSafetyMode.PublicationOnly);
        DirectoryPaths = new Lazy<ImmutableArray<string>>(() => Directories.Value.Select(static d => d.Path).ToImmutableArray(), LazyThreadSafetyMode.PublicationOnly);
        FilesByName = new Lazy<IReadOnlyDictionary<string, string>>(() => RuntimeAssemblies.IndexFiles(this), LazyThreadSafetyMode.PublicationOnly);
        Assemblies = new Lazy<ImmutableArray<RuntimeAssembly>>(() => RuntimeAssemblies.ScanAssemblies(this), LazyThreadSafetyMode.PublicationOnly);
        Presets = new Lazy<ImmutableArray<AssemblyListPreset>>(AssemblyListPresets.FindInstalled);
    }

    internal Lazy<ImmutableArray<(string Framework, string Path)>> Directories { get; }

    internal Lazy<ImmutableArray<string>> DirectoryPaths { get; }

    internal Lazy<IReadOnlyDictionary<string, string>> FilesByName { get; }

    internal Lazy<ImmutableArray<RuntimeAssembly>> Assemblies { get; }

    internal Lazy<ImmutableArray<AssemblyListPreset>> Presets { get; }
}
