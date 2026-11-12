using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>A type of the open assemblies that derives directly from the type a folder of derived types
/// belongs to, and the reference it is navigated to by.</summary>
public sealed class DerivedTypeInfo
{
    public required NodeVisibility Visibility { get; init; }

    /// <summary>The module whose tree lists the type, under a type of its own.</summary>
    public required LoadedModule Module { get; init; }

    public required ITypeDefinition Symbol { get; init; }

    public required CodeReference.Entity Reference { get; init; }
}
