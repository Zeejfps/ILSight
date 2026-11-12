namespace ILSight.BookmarkArea;

/// <summary>A folded region left open, as a settings or bookmarks file can hold it.</summary>
internal sealed record BookmarkFoldingRangeJson
{
    public int Start { get; init; }

    public int End { get; init; }
}
