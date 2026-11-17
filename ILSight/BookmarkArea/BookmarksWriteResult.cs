namespace ILSight.BookmarkArea;

/// <summary>What writing a bookmarks file did.</summary>
public union BookmarksWriteResult(BookmarksWriteResult.Written, BookmarksWriteResult.Failed)
{
    public sealed record Written;

    /// <summary><see cref="Reason"/> names the file and says why, as the system said it.</summary>
    public sealed record Failed
    {
        public required string Reason { get; init; }
    }
}
