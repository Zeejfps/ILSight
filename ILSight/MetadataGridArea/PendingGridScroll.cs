namespace ILSight.MetadataGridArea;

/// <summary>Where a metadata grid is to go once it is laid out: to a row selected and scrolled into view, or
/// back to where it stood.</summary>
internal union PendingGridScroll(PendingGridScroll.Reveal, PendingGridScroll.Restore)
{
    public sealed record Reveal(int Row);

    public sealed record Restore(GridPosition Position);
}
