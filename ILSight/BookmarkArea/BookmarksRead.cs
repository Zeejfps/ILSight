using System.Diagnostics;

namespace ILSight.BookmarkArea;

/// <summary>What reading a bookmarks file gave.</summary>
public abstract record BookmarksRead
{
    private BookmarksRead()
    {
    }

    /// <summary>The file was read: the bookmarks in it that name an assembly file, and what was wrong
    /// with the entries left out, or whose place in this app's tree was.</summary>
    public sealed record Read : BookmarksRead
    {
        public required List<BookmarkRecord> Bookmarks { get; init; }

        public required IReadOnlyList<BookmarkEntryProblem> Problems { get; init; }
    }

    /// <summary>The file is JSON, but holds no list of bookmarks.</summary>
    public sealed record NotBookmarksFile : BookmarksRead;

    /// <summary>The file could not be read, or is not JSON. <see cref="Reason"/> names the file and says
    /// why, as the system or the JSON reader said it.</summary>
    public sealed record Failed : BookmarksRead
    {
        public required string Reason { get; init; }
    }

    public TResult Match<TResult>(Func<Read, TResult> read, Func<NotBookmarksFile, TResult> notBookmarksFile, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Read r => read(r),
            NotBookmarksFile n => notBookmarksFile(n),
            Failed f => failed(f),
            _ => throw new UnreachableException($"BookmarksRead.Match does not handle {GetType().Name}."),
        };
    }
}
