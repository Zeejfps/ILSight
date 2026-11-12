namespace ILSight.CodeViewArea;

/// <summary>Where a code view is to scroll once it is laid out: to a line revealed and flashed, back to where
/// it stood, to a find match, or to the caret.</summary>
internal abstract record PendingScroll
{
    private PendingScroll()
    {
    }

    /// <summary>Scroll <see cref="Line"/> to about a third from the top, expanding the folds that hide it, and flash it.</summary>
    public sealed record Reveal(int Line) : PendingScroll;

    public sealed record Restore(CodeViewPosition Position) : PendingScroll;

    /// <summary>Bring the columns from <see cref="ColumnStart"/> to <see cref="ColumnEnd"/> of <see cref="Line"/> into view.</summary>
    public sealed record ShowMatch(int Line, int ColumnStart, int ColumnEnd) : PendingScroll;

    /// <summary>Scroll as little as brings the caret into view, after a key moved it.</summary>
    public sealed record ShowCaret : PendingScroll;
}
