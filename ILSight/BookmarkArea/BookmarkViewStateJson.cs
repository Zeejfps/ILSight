namespace ILSight.BookmarkArea;

/// <summary>ILSpy's view of a bookmark's document, as a settings or bookmarks file can hold it.</summary>
internal sealed record BookmarkViewStateJson
{
    public int Version { get; init; }

    public int CaretOffset { get; init; }

    public double VerticalOffset { get; init; }

    public double HorizontalOffset { get; init; }

    public int? FoldingChecksum { get; init; }

    public List<BookmarkFoldingRangeJson?>? ExpandedFoldings { get; init; }

    public BookmarkRenderedLayoutSettingsJson? RenderedLayoutSettings { get; init; }

    public List<string?>? SelectedTreeNodePath { get; init; }
}
