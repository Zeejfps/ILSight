namespace ILSight.BookmarkArea;

/// <summary>How the real ILSpy anchors a bookmark: to a definition's metadata token, to an IL offset
/// in a method body, or to a line number of the document of a node.</summary>
public enum BookmarkKind
{
    Token,
    Body,
    Line,
}
