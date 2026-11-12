using System.Collections.Concurrent;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>An assembly or WebCIL module that was read: a file on disk, or, when <see cref="Place"/>
/// says so, one read out of a package, known by the path <see cref="AssemblyPackages.GetFilePath"/> gives it.
/// What takes reading is held as a <see cref="Lazy{T}"/>, read once whichever thread asks first, and asked
/// for with the <c>Ensure</c> functions of <see cref="LoadedAssemblies"/>.</summary>
public sealed class LoadedModule : LoadedAssembly
{
    public required Lazy<DecompilerTypeSystem> TypeSystem { get; init; }

    /// <summary>The assembly's portable PDB; null when it has none.</summary>
    public required Lazy<DebugSymbols?> DebugSymbols { get; init; }

    /// <summary>Whether a PDB can be generated for this assembly: only one whose debug directory
    /// names a PDB, which the generated one must match.</summary>
    public required Lazy<bool> CanGeneratePdb { get; init; }

    /// <summary>What the assembly uses of the assemblies it references.</summary>
    public required Lazy<ReferenceUsage> ReferenceUsage { get; init; }

    /// <summary>The type systems of the settings that turn language features on or off that
    /// <see cref="TypeSystem"/> models.</summary>
    public readonly ConcurrentDictionary<TypeSystemOptions, Lazy<DecompilerTypeSystem>> TypeSystems = new();

    /// <summary>The package this was read out of, and its file there; null for a file on disk.</summary>
    public required PackagePlace? Place { get; init; }

    public required MetadataFile File { get; init; }

    /// <summary><see cref="File"/> as a portable executable; null for a WebCIL module, which has no PE headers.</summary>
    internal PEFile? PEFile => File as PEFile;

    public required AssemblyResolver Resolver { get; init; }

    /// <summary>The target framework the assembly was built for, e.g. <c>.NETCoreApp,Version=v10.0</c>,
    /// or null if it does not say.</summary>
    public required TargetFrameworkId? DetectedTargetFramework { get; init; }

    /// <summary>The framework the assembly's references are resolved against: the one the list picked
    /// for it, else <see cref="DetectedTargetFramework"/>.</summary>
    public required TargetFrameworkId? TargetFramework { get; init; }

    /// <summary>Whether the list picked <see cref="TargetFramework"/> for this assembly.</summary>
    public required bool IsTargetFrameworkPicked { get; init; }

    /// <summary><see cref="TargetFramework"/> as ILSpy shows it, e.g. <c>.NETCoreApp, v10.0</c>.</summary>
    public string? FrameworkName
    {
        get
        {
            if (TargetFramework is null)
            {
                return null;
            }

            return TargetFramework.Readable;
        }
    }

    /// <summary><see cref="TargetFramework"/> as a moniker, e.g. <c>net10.0</c>, <c>netstandard2.0</c>
    /// or <c>net48</c>.</summary>
    public string? FrameworkMoniker
    {
        get
        {
            if (TargetFramework is null)
            {
                return null;
            }

            return TargetFramework.Moniker;
        }
    }

    /// <summary>Whether a PDB can be picked for this assembly: one that loaded with PE headers, which
    /// a WebCIL module lacks.</summary>
    public bool CanPickPdb => PEFile is not null;
}
