using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>Finding what an assembly references: for the decompiler, which asks through its IAssemblyResolver,
/// and for the list.</summary>
public sealed class AssemblyResolver : IAssemblyResolver
{
    private static readonly string[] Extensions = [".dll", ".exe", ".winmd", ".wasm"];

    public required AssemblyList List { get; init; }
    public required string MainPath { get; init; }

    /// <summary>The package the main assembly was read out of, whose folders are looked in first, from the
    /// main assembly's outwards; null for a file on disk.</summary>
    public required AssemblyPackage? Package { get; init; }

    public required TargetFrameworkId? TargetFramework { get; init; }
    public required Lazy<UniversalAssemblyResolver> Fallback { get; init; }

    /// <summary>The resolver of what the assembly at <paramref name="mainPath"/>, of <paramref name="list"/>, references.</summary>
    internal static AssemblyResolver Create(AssemblyList list, AssemblyPath mainPath, TargetFrameworkId? targetFramework, AssemblyPackage? package)
    {
        return new AssemblyResolver
        {
            List = list,
            MainPath = mainPath.Text,
            Package = package,
            TargetFramework = targetFramework,
            Fallback = new Lazy<UniversalAssemblyResolver>(() =>
                new UniversalAssemblyResolver(mainPath.Text, false, targetFramework?.Text, null, PEStreamOptions.PrefetchEntireImage, MetadataReaderOptions.ApplyWindowsRuntimeProjections)),
        };
    }

    public MetadataFile? Resolve(IAssemblyReference reference)
    {
        return ResolveAssembly(reference)?.File;
    }

    public IDisposable? BeginSnapshot()
    {
        return null;
    }

    public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName)
    {
        var directory = Path.GetDirectoryName(mainModule.FileName);
        if (directory is null)
        {
            return null;
        }

        var path = AssemblyPath.Parse(Path.Combine(directory, moduleName));
        if (path is null)
        {
            return null;
        }

        return Exists(path) ? AssemblyListing.TryLoadAssembly(List, path)?.File : null;
    }

    public Task<MetadataFile?> ResolveAsync(IAssemblyReference reference)
    {
        return Task.FromResult(Resolve(reference));
    }

    public Task<MetadataFile?> ResolveModuleAsync(MetadataFile mainModule, string moduleName)
    {
        return Task.FromResult(ResolveModule(mainModule, moduleName));
    }

    /// <summary>The assembly <paramref name="reference"/> names: read from its file, when the list loads
    /// references, as <see cref="FindAssembly"/> finds it; else only one listed.</summary>
    internal LoadedModule? ResolveAssembly(IAssemblyReference reference)
    {
        if (!List.LoadsReferences)
        {
            return FindListed(reference);
        }

        return FindAssembly(reference);
    }

    /// <summary>The listed assembly <paramref name="reference"/> names, one as new as it asks for first.</summary>
    private LoadedModule? FindListed(IAssemblyReference reference)
    {
        var exact = AssemblyListing.FindKnown(List, reference, exactOrNewer: true, listedOnly: true);
        if (exact is not null)
        {
            return exact;
        }

        return AssemblyListing.FindKnown(List, reference, exactOrNewer: false, listedOnly: true);
    }

    /// <summary>The assembly <paramref name="reference"/> names, one read already as new as it asks for,
    /// else read from its file, else any read of its name; for loading it whatever the list's options.</summary>
    internal LoadedModule? FindAssembly(IAssemblyReference reference)
    {
        var known = AssemblyListing.FindKnown(List, reference, exactOrNewer: true);
        if (known is not null)
        {
            return known;
        }

        var path = FindAssemblyFile(reference);
        var loaded = path is not null ? AssemblyListing.TryLoadAssembly(List, path) : null;
        if (loaded is not null)
        {
            return loaded;
        }

        return AssemblyListing.FindKnown(List, reference, exactOrNewer: false);
    }

    internal AssemblyPath? FindAssemblyFile(IAssemblyReference reference)
    {
        foreach (var directory in ListNearbyDirectories())
        {
            var found = FindIn(directory, reference.Name);
            if (found is not null)
            {
                return found;
            }
        }
        if (FrameworkIds.RunsOnCore(TargetFramework))
        {
            var runtime = FindInRuntime(reference.Name);
            if (runtime is not null)
            {
                return runtime;
            }
        }
        try
        {
            var fallback = Fallback.Value.FindAssemblyFile(reference);
            var found = fallback is null ? null : AssemblyPath.Parse(fallback);
            if (found is not null)
            {
                return found;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException)
        {
            return null;
        }
        // The fallback's last resort is the directory of the runtime it runs on, which a native build
        // does not have: it answers null, or "<Unknown>" for that path. The installed runtime stands in.
        return FindInRuntime(reference.Name);
    }

    /// <summary>The running runtime's file of <paramref name="name"/>; null when it has none.</summary>
    private AssemblyPath? FindInRuntime(string name)
    {
        var found = RuntimeAssemblies.Find(List.Runtime, name);
        if (found is null)
        {
            return null;
        }

        return AssemblyPath.Parse(found);
    }

    /// <summary>The main assembly's directory; for one read out of a package, then each folder of the
    /// package above it, up to the package's root.</summary>
    private IEnumerable<string> ListNearbyDirectories()
    {
        var directory = Path.GetDirectoryName(MainPath);
        if (directory is null)
        {
            yield break;
        }

        yield return directory;
        if (Package is null)
        {
            yield break;
        }

        while (directory.Length > Package.FilePath.Text.Length)
        {
            directory = Path.GetDirectoryName(directory);
            if (directory is null)
            {
                yield break;
            }

            yield return directory;
        }
    }

    private AssemblyPath? FindIn(string directory, string name)
    {
        foreach (var extension in Extensions)
        {
            var candidate = AssemblyPath.Parse(Path.Combine(directory, name + extension));
            if (candidate is not null && Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>Whether there is a file at <paramref name="path"/>, on disk or in the main assembly's package.</summary>
    private bool Exists(AssemblyPath path)
    {
        if (File.Exists(path.Text))
        {
            return true;
        }

        if (Package is null)
        {
            return false;
        }

        return AssemblyPackages.FindFileAt(Package, path) is not null;
    }
}
