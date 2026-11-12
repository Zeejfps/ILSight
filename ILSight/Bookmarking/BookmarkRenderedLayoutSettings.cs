namespace ILSight.Bookmarking;

/// <summary>ILSpy's display settings that shaped the text the bookmark was made in.</summary>
public sealed record BookmarkRenderedLayoutSettings(
    bool FoldBraces,
    bool ExpandXmlDocumentationComments,
    bool ExpandMemberDefinitions,
    bool ExpandUsingDeclarations,
    bool ShowDebugInfo,
    bool IndentationUseTabs,
    int IndentationSize,
    int IndentationTabSize);
