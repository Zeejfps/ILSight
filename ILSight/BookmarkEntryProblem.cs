using System.Diagnostics;

namespace ILSight;

/// <summary>What was wrong with one entry of a bookmarks file, for the screen to tell in the chosen
/// language. <see cref="Entry"/> is the entry's position in the file, from 1.</summary>
public abstract record BookmarkEntryProblem
{
    private BookmarkEntryProblem()
    {
    }

    public required int Entry { get; init; }

    /// <summary>The entry is a bookmark, but the place in this app's tree it was made in cannot be read,
    /// so the bookmark is placed as ILSpy places it.</summary>
    public sealed record PlaceUnreadable : BookmarkEntryProblem;

    /// <summary>The entry is not a bookmark at all, so it was left out. <see cref="Detail"/> is as the
    /// JSON reader said it.</summary>
    public sealed record NotABookmark : BookmarkEntryProblem
    {
        public required string Detail { get; init; }
    }

    public TResult Match<TResult>(Func<PlaceUnreadable, TResult> placeUnreadable, Func<NotABookmark, TResult> notABookmark)
    {
        return this switch
        {
            PlaceUnreadable p => placeUnreadable(p),
            NotABookmark n => notABookmark(n),
            _ => throw new UnreachableException($"BookmarkEntryProblem.Match does not handle {GetType().Name}."),
        };
    }
}
