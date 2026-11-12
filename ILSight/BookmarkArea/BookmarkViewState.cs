namespace ILSight.BookmarkArea;

/// <summary>ILSpy's view of the document when the bookmark was made; <see cref="SelectedTreeNodePath"/>
/// is the path ILSpy's tree finds the bookmark's node by, which ILSpy needs to go to it.</summary>
public sealed record BookmarkViewState
{
    public required int Version { get; init; }

    public required int CaretOffset { get; init; }

    public required double VerticalOffset { get; init; }

    public required double HorizontalOffset { get; init; }

    public required int? FoldingChecksum { get; init; }

    public required List<BookmarkFoldingRange>? ExpandedFoldings { get; init; }

    public BookmarkRenderedLayoutSettings? RenderedLayoutSettings { get; init; }

    public List<string>? SelectedTreeNodePath { get; init; }
}
