using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>A type a module defines, with the handle its node is found by and the reference it is navigated to by.</summary>
public sealed class DefinedType
{
    public required LoadedModule Module { get; init; }

    public required ITypeDefinition Symbol { get; init; }

    public required TypeDefinitionHandle Handle { get; init; }

    public required CodeReference.Entity Reference { get; init; }

    public TypeShape Shape => EntityText.ShapeOf(Symbol);

    /// <summary>How visible the type is: compiler-generated when the compiler made it up, or a type it is nested in.</summary>
    public required NodeVisibility Visibility { get; init; }
}
