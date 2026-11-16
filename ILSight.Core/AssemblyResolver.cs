using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>Finding what an assembly references: for the library, which asks within a unit through a
/// <see cref="ReadingResolver"/>, and for the list.</summary>
public sealed class AssemblyResolver
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

    /// <summary>The module <paramref name="moduleName"/> names, a file beside the main module's at
    /// <paramref name="mainModuleFileName"/>, read;
    /// null when there is none (unresolved only for not there); a file there that can't be read throws its
    /// <see cref="AssemblyLoadException"/>, with why: the library's resolver reads that reference by name only and
    /// notes the failure (<see cref="ReadingResolver"/>).</summary>
    internal LoadedModule? FindModule(string mainModuleFileName, string moduleName)
    {
        var directory = Path.GetDirectoryName(mainModuleFileName);
        if (directory is null)
        {
            return null;
        }

        var path = AssemblyPath.Parse(Path.Combine(directory, moduleName));
        if (path is null)
        {
            return null;
        }

        if (!Exists(path))
        {
            return null;
        }

        return AssemblyListing.FindAssemblyToLookIn(List, path);
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

    private LoadedModule? FindListed(IAssemblyReference reference)
    {
        var exact = List.FindKnown(reference, exactOrNewer: true, listedOnly: true);
        if (exact is not null)
        {
            return exact;
        }

        return List.FindKnown(reference, exactOrNewer: false, listedOnly: true);
    }

    /// <summary>The assembly <paramref name="reference"/> names, one read already as new as it asks for,
    /// else read from its file, else any read of its name; for loading it whatever the list's options. A file found for
    /// it that can't be read throws its <see cref="AssemblyLoadException"/>: not there is the only unresolved.</summary>
    internal LoadedModule? FindAssembly(IAssemblyReference reference)
    {
        var begun = BeginFindAssembly(reference);
        if (begun.Done)
        {
            return begun.Found;
        }

        return EndFindAssembly(reference, ProbeLibrary(reference));
    }

    /// <summary><see cref="ResolveAssembly"/> up to the library's own search (<see cref="ProbeLibrary"/>), which a
    /// caller keeping the library's work apart from the app's runs between this and <see cref="EndResolveAssembly"/>
    /// (<see cref="ReadingResolver"/>, CF9): done, with what was found, when it needs no search of the library's.</summary>
    internal (bool Done, LoadedModule? Found) BeginResolveAssembly(IAssemblyReference reference)
    {
        if (!List.LoadsReferences)
        {
            return (true, FindListed(reference));
        }

        return BeginFindAssembly(reference);
    }

    /// <summary><see cref="ResolveAssembly"/> after the library's search answered <paramref name="probed"/>.</summary>
    internal LoadedModule? EndResolveAssembly(IAssemblyReference reference, LibraryProbe probed)
    {
        return EndFindAssembly(reference, probed);
    }

    private (bool Done, LoadedModule? Found) BeginFindAssembly(IAssemblyReference reference)
    {
        var known = List.FindKnown(reference, exactOrNewer: true, listedOnly: false);
        if (known is not null)
        {
            return (true, known);
        }

        var path = FindNearby(reference);
        if (path is null)
        {
            return (false, null);
        }

        return (true, FindLoadedOrKnown(reference, path));
    }

    private LoadedModule? EndFindAssembly(IAssemblyReference reference, LibraryProbe probed)
    {
        return FindLoadedOrKnown(reference, FindAfterProbe(reference, probed));
    }

    private LoadedModule? FindLoadedOrKnown(IAssemblyReference reference, AssemblyPath? path)
    {
        var loaded = path is not null ? AssemblyListing.FindAssemblyToLookIn(List, path) : null;
        if (loaded is not null)
        {
            return loaded;
        }

        return List.FindKnown(reference, exactOrNewer: false, listedOnly: false);
    }

    internal AssemblyPath? FindAssemblyFile(IAssemblyReference reference)
    {
        return FindNearby(reference) ?? FindAfterProbe(reference, ProbeLibrary(reference));
    }

    // The folders near the main assembly, then the installed runtime for one that runs on .NET Core.
    private AssemblyPath? FindNearby(IAssemblyReference reference)
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
            return FindInRuntime(reference.Name);
        }

        return null;
    }

    /// <summary>What the library's own resolver (<see cref="Fallback"/>) finds for <paramref name="reference"/>: its
    /// search of the disk and of the main assembly's runtime files, the library's code.</summary>
    internal LibraryProbe ProbeLibrary(IAssemblyReference reference)
    {
        try
        {
            var fallback = Fallback.Value.FindAssemblyFile(reference);
            return new LibraryProbe { Found = fallback is null ? null : AssemblyPath.Parse(fallback), Failed = false };
        }
        catch (IOException)
        {
            return LibraryProbe.Failure;
        }
        catch (UnauthorizedAccessException)
        {
            return LibraryProbe.Failure;
        }
        catch (BadImageFormatException)
        {
            return LibraryProbe.Failure;
        }
        catch (InvalidOperationException)
        {
            return LibraryProbe.Failure;
        }
    }

    private AssemblyPath? FindAfterProbe(IAssemblyReference reference, LibraryProbe probed)
    {
        if (probed.Failed)
        {
            return null;
        }

        // The fallback's last resort is the directory of the runtime it runs on, which a native build
        // does not have: it answers null, or "<Unknown>" for that path. The installed runtime stands in.
        return probed.Found ?? FindInRuntime(reference.Name);
    }

    private AssemblyPath? FindInRuntime(string name)
    {
        var found = RuntimeAssemblies.Find(List.Runtime, name);
        if (found is null)
        {
            return null;
        }

        return AssemblyPath.Parse(found);
    }

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
