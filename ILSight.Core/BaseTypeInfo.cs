namespace ILSight.Core;

/// <summary>A type a type derives from directly, as <see cref="Module"/> names it, and what it resolves to: a handle,
/// holding none of the library's objects (the I/O side keeps the type and its definition against it).</summary>
public sealed class BaseTypeInfo
{
    public required LoadedModule Module { get; init; }

    public required BaseTypeTarget Target { get; init; }

    /// <summary>How visible the type is, when it can be navigated to; else as visible as anything is.</summary>
    public required NodeVisibility Visibility { get; init; }
}
