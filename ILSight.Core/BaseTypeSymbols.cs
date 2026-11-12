using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The library's objects a base type's node stands for, kept against its <see cref="BaseTypeInfo"/> by the I/O
/// side: the type as the module names it, and its definition when it is found.</summary>
internal sealed class BaseTypeSymbols
{
    public required IType Type { get; init; }

    public required ITypeDefinition? Definition { get; init; }
}
