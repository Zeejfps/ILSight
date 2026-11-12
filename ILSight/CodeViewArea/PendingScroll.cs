using System.Diagnostics;

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
    public sealed record ShowMatch : PendingScroll
    {
        public required int Line { get; init; }

        public required int ColumnStart { get; init; }

        public required int ColumnEnd { get; init; }
    }

    /// <summary>Scroll as little as brings the caret into view, after a key moved it.</summary>
    public sealed record ShowCaret : PendingScroll;

    public TResult Match<TResult>(Func<Reveal, TResult> reveal, Func<Restore, TResult> restore, Func<ShowMatch, TResult> showMatch,
        Func<ShowCaret, TResult> showCaret)
    {
        return this switch
        {
            Reveal r => reveal(r),
            Restore r => restore(r),
            ShowMatch m => showMatch(m),
            ShowCaret c => showCaret(c),
            _ => throw new UnreachableException($"PendingScroll.Match does not handle {GetType().Name}."),
        };
    }

    public void Switch(Action<Reveal> reveal, Action<Restore> restore, Action<ShowMatch> showMatch, Action<ShowCaret> showCaret)
    {
        switch (this)
        {
            case Reveal r:
                reveal(r);
                break;
            case Restore r:
                restore(r);
                break;
            case ShowMatch m:
                showMatch(m);
                break;
            case ShowCaret c:
                showCaret(c);
                break;
            default:
                throw new UnreachableException($"PendingScroll.Switch does not handle {GetType().Name}.");
        }
    }
}
