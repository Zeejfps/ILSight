using System.Diagnostics;

namespace ILSight.Core;

internal abstract record UsedEntry
{
    private UsedEntry()
    {
    }

    public sealed record Type(string FullName, CodeReference? Target, IReadOnlyList<UsedEntry> Used) : UsedEntry;

    public sealed record Member(string Label, SpanKind Kind, CodeReference? Target) : UsedEntry;

    public sealed record Forwarded(ForwardedLine Line) : UsedEntry;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which
    /// it is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Type> type, Action<TState, Member> member,
        Action<TState, Forwarded> forwarded)
    {
        switch (this)
        {
            case Type c:
                type(state, c);
                return;
            case Member c:
                member(state, c);
                return;
            case Forwarded c:
                forwarded(state, c);
                return;
            default:
                throw new UnreachableException($"UsedEntry.Switch does not handle {GetType().Name}.");
        }
    }
}
