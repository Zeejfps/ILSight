namespace ILSight;

/// <summary>Where a metadata grid is to go once it is laid out: to a row selected and scrolled into view, or
/// back to where it stood.</summary>
internal abstract record PendingGridScroll
{
    private PendingGridScroll()
    {
    }

    public sealed record Reveal(int Row) : PendingGridScroll;

    public sealed record Restore(GridPosition Position) : PendingGridScroll;
}
