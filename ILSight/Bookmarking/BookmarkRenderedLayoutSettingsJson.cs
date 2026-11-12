namespace ILSight.Bookmarking;

/// <summary>ILSpy's display settings a bookmark was made in, as a settings or bookmarks file can hold them.</summary>
internal sealed record BookmarkRenderedLayoutSettingsJson(
    bool FoldBraces,
    bool ExpandXmlDocumentationComments,
    bool ExpandMemberDefinitions,
    bool ExpandUsingDeclarations,
    bool ShowDebugInfo,
    bool IndentationUseTabs,
    int IndentationSize,
    int IndentationTabSize);
