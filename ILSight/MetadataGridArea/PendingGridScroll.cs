using System.Diagnostics;

namespace ILSight.MetadataGridArea;

/// <summary>Where a metadata grid is to go once it is laid out: to a row selected and scrolled into view, or
/// back to where it stood.</summary>
internal abstract record PendingGridScroll
{
    private PendingGridScroll()
    {
    }

    public sealed record Reveal(int Row) : PendingGridScroll;

    public sealed record Restore(GridPosition Position) : PendingGridScroll;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which it
    /// is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Reveal> reveal, Action<TState, Restore> restore)
    {
        switch (this)
        {
            case Reveal c:
                reveal(state, c);
                return;
            case Restore c:
                restore(state, c);
                return;
            default:
                throw new UnreachableException($"PendingGridScroll.Switch does not handle {GetType().Name}.");
        }
    }
}
