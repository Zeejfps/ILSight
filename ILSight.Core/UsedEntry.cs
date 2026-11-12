using System.Diagnostics;

namespace ILSight.Core;

internal abstract record UsedEntry
{
    private UsedEntry()
    {
    }

    public sealed record Type : UsedEntry
    {
        public required string FullName { get; init; }

        public required CodeReference? Target { get; init; }

        public required IReadOnlyList<UsedEntry> Used { get; init; }
    }

    public sealed record Member : UsedEntry
    {
        public required string Label { get; init; }

        public required SpanKind Kind { get; init; }

        public required CodeReference? Target { get; init; }
    }

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
