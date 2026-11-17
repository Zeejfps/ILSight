namespace ILSight.BookmarkArea;

/// <summary>What was wrong with one entry of a bookmarks file, for the screen to tell in the chosen
/// language. Each case's <c>Entry</c> is the entry's position in the file, from 1.</summary>
public union BookmarkEntryProblem(BookmarkEntryProblem.PlaceUnreadable, BookmarkEntryProblem.NotABookmark)
{
    /// <summary>The entry is a bookmark, but the place in this app's tree it was made in cannot be read,
    /// so the bookmark is placed as ILSpy places it.</summary>
    public sealed record PlaceUnreadable
    {
        public required int Entry { get; init; }
    }

    /// <summary>The entry is not a bookmark at all, so it was left out. <see cref="Detail"/> is as the
    /// JSON reader said it.</summary>
    public sealed record NotABookmark
    {
        public required int Entry { get; init; }

        public required string Detail { get; init; }
    }
}
