namespace ILSight.Bookmarking;

/// <summary>ILSpy's view of the document when the bookmark was made; <see cref="SelectedTreeNodePath"/>
/// is the path ILSpy's tree finds the bookmark's node by, which ILSpy needs to go to it.</summary>
public sealed record BookmarkViewState(
    int Version,
    int CaretOffset,
    double VerticalOffset,
    double HorizontalOffset,
    int? FoldingChecksum,
    List<BookmarkFoldingRange>? ExpandedFoldings,
    BookmarkRenderedLayoutSettings? RenderedLayoutSettings = null,
    List<string>? SelectedTreeNodePath = null);
