
namespace ILSight.Core;

/// <summary>A type of the open assemblies that derives directly from the type a folder of derived types
/// belongs to, and the reference it is navigated to by: a handle, holding none of the library's objects (the I/O
/// side keeps the type against it).</summary>
public sealed class DerivedTypeInfo
{
    public required NodeVisibility Visibility { get; init; }

    /// <summary>The module whose tree lists the type, under a type of its own.</summary>
    public required LoadedModule Module { get; init; }

    public required CodeReference.Entity Reference { get; init; }
}
