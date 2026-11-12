using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>A member a module defines, with the handle its node is found by and the reference it is navigated to by.</summary>
public sealed class DefinedMember
{
    public required LoadedModule Module { get; init; }

    public required IMember Symbol { get; init; }

    public EntityHandle Handle => Reference.Handle;

    public required CodeReference.Entity Reference { get; init; }

    public MemberShape Shape => EntityText.ShapeOf(Symbol);

    /// <summary>How visible the member is: compiler-generated when the compiler made it up, or the property,
    /// event or type that holds it.</summary>
    public required NodeVisibility Visibility { get; init; }
}
