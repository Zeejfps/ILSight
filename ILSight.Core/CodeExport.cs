using System.Diagnostics;
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

    public sealed record Namespace : CodeExport
    {
        public required LoadedModule Module { get; init; }

        public required string Name { get; init; }

        public required IReadOnlyList<ITypeDefinition> Types { get; init; }
    }

    public sealed record Document(DocumentRequest Request) : CodeExport;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which
    /// it is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, WholeModule> wholeModule,
        Action<TState, Namespace> @namespace, Action<TState, Document> document)
    {
        switch (this)
        {
            case WholeModule c:
                wholeModule(state, c);
                return;
            case Namespace c:
                @namespace(state, c);
                return;
            case Document c:
                document(state, c);
                return;
            default:
                throw new UnreachableException($"CodeExport.Switch does not handle {GetType().Name}.");
        }
    }
}
