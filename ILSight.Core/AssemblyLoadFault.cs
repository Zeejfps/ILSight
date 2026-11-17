namespace ILSight.Core;

/// <summary>Why a file can't be opened as a .NET assembly or module, as ILSight's loader finds it
/// (<see cref="AssemblyListing"/>): a cause the app words in each language. The file it is about is the exception's
/// <see cref="AssemblyLoadException.FilePath"/>; what the system or the library said, where one did, its inner
/// exception.</summary>
public union AssemblyLoadFault(AssemblyLoadFault.CannotOpen, AssemblyLoadFault.NotAssembly, AssemblyLoadFault.NotPackage,
    AssemblyLoadFault.NotInPackage)
{
    /// <summary>The file couldn't be opened or read as an image: the system or the library said why, in the
    /// exception's inner exception.</summary>
    public sealed record CannotOpen;

    /// <summary>The file is an image with no .NET metadata.</summary>
    public sealed record NotAssembly;

    /// <summary>The path names a file in a package, and the file it names as the package is no package.</summary>
    public sealed record NotPackage
    {
        public required AssemblyPath Container { get; init; }
    }

    /// <summary>The package holds no file of the name the path gives.</summary>
    public sealed record NotInPackage
    {
        public required AssemblyPath Package { get; init; }

        public required string FullName { get; init; }
    }
}
