namespace ILSight.CodeViewArea;

/// <summary>Where a code view is to scroll once it is laid out: to a line revealed and flashed, back to where
/// it stood, to a find match, or to the caret.</summary>
internal union PendingScroll(PendingScroll.Reveal, PendingScroll.Restore, PendingScroll.ShowMatch,
    PendingScroll.ShowCaret)
{
    /// <summary>Scroll <see cref="Line"/> to about a third from the top, expanding the folds that hide it, and flash it.</summary>
    public sealed record Reveal(int Line);

    public sealed record Restore(CodeViewPosition Position);

    /// <summary>Bring the columns from <see cref="ColumnStart"/> to <see cref="ColumnEnd"/> of <see cref="Line"/> into view.</summary>
    public sealed record ShowMatch
    {
        public required int Line { get; init; }

        public required int ColumnStart { get; init; }

        public required int ColumnEnd { get; init; }
    }

    /// <summary>Scroll as little as brings the caret into view, after a key moved it.</summary>
    public sealed record ShowCaret;
}
