using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>What saving a node's code writes: a whole module, every type of a namespace, or the
/// document the node shows.</summary>
internal union CodeExport(CodeExport.WholeModule, CodeExport.Namespace, CodeExport.Document)
{
    public sealed record WholeModule(LoadedModule Module);

    public sealed record Namespace
    {
        public required LoadedModule Module { get; init; }

        public required string Name { get; init; }

        public required IReadOnlyList<ITypeDefinition> Types { get; init; }
    }

    public sealed record Document(DocumentRequest Request);
}
