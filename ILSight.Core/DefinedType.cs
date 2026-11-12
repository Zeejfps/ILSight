using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>A type a module defines, with the handle its node is found by and the reference it is navigated to by: a
/// handle, holding none of the library's objects (the I/O side keeps the type against it,
/// <see cref="NodeDefinitions.GetSymbol(DefinedType)"/>).</summary>
public sealed class DefinedType
{
    public required LoadedModule Module { get; init; }

    public required TypeDefinitionHandle Handle { get; init; }

    public required CodeReference.Entity Reference { get; init; }

    public required TypeShape Shape { get; init; }

    /// <summary>How visible the type is: compiler-generated when the compiler made it up, or a type it is nested in.</summary>
    public required NodeVisibility Visibility { get; init; }

    /// <summary>The type's name, without its type parameters.</summary>
    public required string Name { get; init; }

    /// <summary>How many type parameters the type declares itself.</summary>
    public required int Arity { get; init; }
}
