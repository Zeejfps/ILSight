using System.Diagnostics;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

internal abstract record NamespaceEntry
{
    private NamespaceEntry()
    {
    }

    public sealed record Declared(ITypeDefinition Type, string Label, CodeReference? Reference) : NamespaceEntry;

    public sealed record Forwarded(ForwardedLine Line) : NamespaceEntry;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which
    /// it is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Declared> declared, Action<TState, Forwarded> forwarded)
    {
        switch (this)
        {
            case Declared c:
                declared(state, c);
                return;
            case Forwarded c:
                forwarded(state, c);
                return;
            default:
                throw new UnreachableException($"NamespaceEntry.Switch does not handle {GetType().Name}.");
        }
    }
}
