namespace ILSight.BookmarkArea;

/// <summary>A folded region left open in ILSpy's view when it made the bookmark.</summary>
public sealed record BookmarkFoldingRange
{
    public required int Start { get; init; }

    public required int End { get; init; }
}
