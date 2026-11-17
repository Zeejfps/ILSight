using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

internal union NamespaceEntry(NamespaceEntry.Declared, NamespaceEntry.Forwarded)
{
    public sealed record Declared
    {
        public required ITypeDefinition Type { get; init; }

        public required string Label { get; init; }

        public required CodeReference? Reference { get; init; }
    }

    public sealed record Forwarded(ForwardedLine Line);
}
