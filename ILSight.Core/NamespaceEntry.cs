using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

internal abstract record NamespaceEntry
{
    private NamespaceEntry()
    {
    }

    public sealed record Declared(ITypeDefinition Type, string Label, CodeReference? Reference) : NamespaceEntry;

    public sealed record Forwarded(ForwardedLine Line) : NamespaceEntry;
}
