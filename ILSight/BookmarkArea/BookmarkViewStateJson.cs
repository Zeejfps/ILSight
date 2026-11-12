namespace ILSight.BookmarkArea;

/// <summary>ILSpy's view of a bookmark's document, as a settings or bookmarks file can hold it.</summary>
internal sealed record BookmarkViewStateJson(
    int Version,
    int CaretOffset,
    double VerticalOffset,
    double HorizontalOffset,
    int? FoldingChecksum,
    List<BookmarkFoldingRangeJson?>? ExpandedFoldings,
    BookmarkRenderedLayoutSettingsJson? RenderedLayoutSettings,
    List<string?>? SelectedTreeNodePath);
