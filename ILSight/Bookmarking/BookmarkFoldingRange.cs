namespace ILSight.Bookmarking;

/// <summary>A folded region left open in ILSpy's view when it made the bookmark.</summary>
public sealed record BookmarkFoldingRange(int Start, int End);
