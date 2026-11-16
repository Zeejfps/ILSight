using System.Diagnostics;

namespace ILSight.Core;

/// <summary>Why a file can't be opened as a .NET assembly or module, as ILSight's loader finds it
/// (<see cref="AssemblyListing"/>): a cause the app words in each language. The file it is about is the exception's
/// <see cref="AssemblyLoadException.FilePath"/>; what the system or the library said, where one did, its inner
/// exception.</summary>
public abstract record AssemblyLoadFault
{
    private AssemblyLoadFault()
    {
    }

    /// <summary>The file couldn't be opened or read as an image: the system or the library said why, in the
    /// exception's inner exception.</summary>
    public sealed record CannotOpen : AssemblyLoadFault;

    /// <summary>The file is an image with no .NET metadata.</summary>
    public sealed record NotAssembly : AssemblyLoadFault;

    /// <summary>The path names a file in a package, and the file it names as the package is no package.</summary>
    public sealed record NotPackage : AssemblyLoadFault
    {
        public required AssemblyPath Container { get; init; }
    }

    /// <summary>The package holds no file of the name the path gives.</summary>
    public sealed record NotInPackage : AssemblyLoadFault
    {
        public required AssemblyPath Package { get; init; }

        public required string FullName { get; init; }
    }

    public TResult Match<TResult>(Func<CannotOpen, TResult> cannotOpen, Func<NotAssembly, TResult> notAssembly, Func<NotPackage, TResult> notPackage,
        Func<NotInPackage, TResult> notInPackage)
    {
        return this switch
        {
            CannotOpen c => cannotOpen(c),
            NotAssembly n => notAssembly(n),
            NotPackage p => notPackage(p),
            NotInPackage i => notInPackage(i),
            _ => throw new UnreachableException($"AssemblyLoadFault.Match does not handle {GetType().Name}."),
        };
    }
}
