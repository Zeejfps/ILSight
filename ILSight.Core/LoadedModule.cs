namespace ILSight.Core;

/// <summary>An assembly or WebCIL module that was read: a file on disk, or, when <see cref="Place"/>
/// says so, one read out of a package, known by the path <see cref="AssemblyPackages.GetFilePath"/> gives it. It is a
/// handle: it holds the values read with it, and none of the library's objects, which
/// <see cref="LoadedAssemblies"/> keeps against it for the I/O side, read through its <c>Ensure</c> functions.</summary>
public sealed class LoadedModule : LoadedAssembly
{
    /// <summary>Whether a PDB can be generated for this assembly: only one whose debug directory
    /// names a PDB, which the generated one must match.</summary>
    public required bool CanGeneratePdb { get; init; }

    /// <summary>Whether a PDB can be picked for this assembly: one that loaded with PE headers, which
    /// a WebCIL module lacks.</summary>
    public required bool CanPickPdb { get; init; }

    /// <summary>Whether its file was read whole into memory when it was read (a PE image), so that reading its
    /// metadata reaches nothing past it; a WebCIL module's file is mapped by the library, each read reaching the
    /// disk.</summary>
    public required bool IsImageInMemory { get; init; }

    /// <summary>The package this was read out of, and its file there; null for a file on disk.</summary>
    public required PackagePlace? Place { get; init; }

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
}
