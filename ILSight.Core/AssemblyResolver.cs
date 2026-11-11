using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>What the decompiler asks to find what an assembly references; its IAssemblyResolver demands the methods
/// that stay here, which forward.</summary>
public sealed class AssemblyResolver : IAssemblyResolver
{
    public required AssemblyList List { get; init; }
    public required string MainPath { get; init; }

    /// <summary>The package the main assembly was read out of, whose folders are looked in first, from the
    /// main assembly's outwards; null for a file on disk.</summary>
    public required AssemblyPackage? Package { get; init; }

    public required TargetFrameworkId? TargetFramework { get; init; }
    public required Lazy<UniversalAssemblyResolver> Fallback { get; init; }

    public MetadataFile? Resolve(IAssemblyReference reference)
    {
        return AssemblyResolving.Resolve(this, reference);
    }

    public IDisposable? BeginSnapshot()
    {
        return null;
    }

    public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName)
    {
        return AssemblyResolving.ResolveModule(this, mainModule, moduleName);
    }

    public Task<MetadataFile?> ResolveAsync(IAssemblyReference reference)
    {
        return AssemblyResolving.ResolveAsync(this, reference);
    }

    public Task<MetadataFile?> ResolveModuleAsync(MetadataFile mainModule, string moduleName)
    {
        return AssemblyResolving.ResolveModuleAsync(this, mainModule, moduleName);
    }
}

/// <summary>Finding what an assembly references.</summary>
internal static class AssemblyResolving
{
    private static readonly string[] Extensions = [".dll", ".exe", ".winmd", ".wasm"];

    /// <summary>The resolver of what the assembly at <paramref name="mainPath"/>, of <paramref name="list"/>, references.</summary>
    public static AssemblyResolver CreateResolver(AssemblyList list, AssemblyPath mainPath, TargetFrameworkId? targetFramework, AssemblyPackage? package)
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

    public static MetadataFile? Resolve(AssemblyResolver resolver, IAssemblyReference reference)
    {
        return ResolveAssembly(resolver, reference)?.File;
    }

    /// <summary>The assembly <paramref name="reference"/> names: read from its file, when the list loads
    /// references, as <see cref="LoadAssembly"/> finds it; else only one listed.</summary>
    public static LoadedModule? ResolveAssembly(AssemblyResolver resolver, IAssemblyReference reference)
    {
        if (!resolver.List.LoadsReferences)
        {
            return FindListed(resolver, reference);
        }

        return LoadAssembly(resolver, reference);
    }

    /// <summary>The listed assembly <paramref name="reference"/> names, one as new as it asks for first.</summary>
    private static LoadedModule? FindListed(AssemblyResolver resolver, IAssemblyReference reference)
    {
        var exact = AssemblyListing.FindKnown(resolver.List, reference, exactOrNewer: true, listedOnly: true);
        if (exact is not null)
        {
            return exact;
        }

        return AssemblyListing.FindKnown(resolver.List, reference, exactOrNewer: false, listedOnly: true);
    }

    /// <summary>The assembly <paramref name="reference"/> names, one read already as new as it asks for,
    /// else read from its file, else any read of its name; for loading it whatever the list's options.</summary>
    public static LoadedModule? LoadAssembly(AssemblyResolver resolver, IAssemblyReference reference)
    {
        var known = AssemblyListing.FindKnown(resolver.List, reference, exactOrNewer: true);
        if (known is not null)
        {
            return known;
        }

        var path = FindAssemblyFile(resolver, reference);
        var loaded = path is not null ? AssemblyListing.TryLoadAssembly(resolver.List, path) : null;
        if (loaded is not null)
        {
            return loaded;
        }

        return AssemblyListing.FindKnown(resolver.List, reference, exactOrNewer: false);
    }

    public static AssemblyPath? FindAssemblyFile(AssemblyResolver resolver, IAssemblyReference reference)
    {
        foreach (var directory in ListNearbyDirectories(resolver))
        {
            var found = FindIn(resolver, directory, reference.Name);
            if (found is not null)
            {
                return found;
            }
        }
        if (FrameworkIds.RunsOnCore(resolver.TargetFramework))
        {
            var runtime = FindInRuntime(resolver, reference.Name);
            if (runtime is not null)
            {
                return runtime;
            }
        }
        try
        {
            var fallback = resolver.Fallback.Value.FindAssemblyFile(reference);
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
        return FindInRuntime(resolver, reference.Name);
    }

    /// <summary>The running runtime's file of <paramref name="name"/>; null when it has none, or its
    /// directories cannot be listed for now.</summary>
    private static AssemblyPath? FindInRuntime(AssemblyResolver resolver, string name)
    {
        try
        {
            var found = RuntimeAssemblies.Find(resolver.List.Runtime, name);
            if (found is null)
            {
                return null;
            }

            return AssemblyPath.Parse(found);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The main assembly's directory; for one read out of a package, then each folder of the
    /// package above it, up to the package's root.</summary>
    private static IEnumerable<string> ListNearbyDirectories(AssemblyResolver resolver)
    {
        var directory = Path.GetDirectoryName(resolver.MainPath);
        if (directory is null)
        {
            yield break;
        }

        yield return directory;
        if (resolver.Package is null)
        {
            yield break;
        }

        while (directory.Length > resolver.Package.FilePath.Text.Length)
        {
            directory = Path.GetDirectoryName(directory);
            if (directory is null)
            {
                yield break;
            }

            yield return directory;
        }
    }

    private static AssemblyPath? FindIn(AssemblyResolver resolver, string directory, string name)
    {
        foreach (var extension in Extensions)
        {
            var candidate = AssemblyPath.Parse(Path.Combine(directory, name + extension));
            if (candidate is not null && FileExists(resolver, candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>Whether there is a file at <paramref name="path"/>, on disk or in the main assembly's package.</summary>
    private static bool FileExists(AssemblyResolver resolver, AssemblyPath path)
    {
        if (File.Exists(path.Text))
        {
            return true;
        }

        if (resolver.Package is null)
        {
            return false;
        }

        return AssemblyPackages.FindFileAt(resolver.Package, path) is not null;
    }

    public static MetadataFile? ResolveModule(AssemblyResolver resolver, MetadataFile mainModule, string moduleName)
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

        return FileExists(resolver, path) ? AssemblyListing.TryLoadAssembly(resolver.List, path)?.File : null;
    }

    public static Task<MetadataFile?> ResolveAsync(AssemblyResolver resolver, IAssemblyReference reference)
    {
        return Task.FromResult(Resolve(resolver, reference));
    }

    public static Task<MetadataFile?> ResolveModuleAsync(AssemblyResolver resolver, MetadataFile mainModule, string moduleName)
    {
        return Task.FromResult(ResolveModule(resolver, mainModule, moduleName));
    }
}
