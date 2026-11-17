namespace ILSight.BookmarkArea;

/// <summary>What reading a bookmarks file gave.</summary>
public union BookmarksRead(BookmarksRead.Read, BookmarksRead.NotBookmarksFile, BookmarksRead.Failed)
{
    /// <summary>The file was read: the bookmarks in it that name an assembly file, and what was wrong
    /// with the entries left out, or whose place in this app's tree was.</summary>
    public sealed record Read
    {
        public required List<BookmarkRecord> Bookmarks { get; init; }

        public required IReadOnlyList<BookmarkEntryProblem> Problems { get; init; }
    }

    /// <summary>The file is JSON, but holds no list of bookmarks.</summary>
    public sealed record NotBookmarksFile;

    /// <summary>The file could not be read, or is not JSON. <see cref="Reason"/> names the file and says
    /// why, as the system or the JSON reader said it.</summary>
    public sealed record Failed
    {
        public required string Reason { get; init; }
    }
}
