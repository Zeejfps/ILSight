using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>A type a type derives from directly, as <see cref="Module"/> names it, and what it resolves to.</summary>
public sealed class BaseTypeInfo
{
    public required LoadedModule Module { get; init; }

    /// <summary>The type as it is derived from, with its type arguments.</summary>
    public required IType Type { get; init; }

    public required BaseTypeTarget Target { get; init; }

    /// <summary>How visible the type is, when it can be navigated to; else as visible as anything is.</summary>
    public required NodeVisibility Visibility { get; init; }
}
