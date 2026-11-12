using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>A member a module defines, with the handle its node is found by and the reference it is navigated to by: a
/// handle, holding none of the library's objects (the I/O side keeps the member against it,
/// <see cref="NodeDefinitions.GetSymbol(DefinedMember)"/>).</summary>
public sealed class DefinedMember
{
    public required LoadedModule Module { get; init; }

    public EntityHandle Handle => Reference.Handle;

    public required CodeReference.Entity Reference { get; init; }

    public required MemberShape Shape { get; init; }

    /// <summary>How visible the member is: compiler-generated when the compiler made it up, or the property,
    /// event or type that holds it.</summary>
    public required NodeVisibility Visibility { get; init; }
}
