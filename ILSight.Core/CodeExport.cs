using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>What saving a node's code writes: a whole module, every type of a namespace, or the
/// document the node shows.</summary>
internal abstract record CodeExport
{
    private CodeExport()
    {
    }

    public sealed record WholeModule(LoadedModule Module) : CodeExport;

    public sealed record Namespace(LoadedModule Module, string Name, IReadOnlyList<ITypeDefinition> Types) : CodeExport;

    public sealed record Document(DocumentRequest Request) : CodeExport;
}
