namespace ILSight.BookmarkArea;

/// <summary>What toggling the bookmark on a line did.</summary>
public union BookmarkToggle(BookmarkToggle.Unchanged, BookmarkToggle.Added, BookmarkToggle.Removed)
{
    /// <summary>Nothing: the line cannot be bookmarked.</summary>
    public sealed record Unchanged;

    /// <summary>Bookmarked the line with <see cref="Bookmark"/>.</summary>
    public sealed record Added(Bookmark Bookmark);

    /// <summary>Removed <see cref="Bookmark"/>, which was in <see cref="Row"/> of the list.</summary>
    public sealed record Removed
    {
        public required Bookmark Bookmark { get; init; }

        public required int Row { get; init; }
    }
}
