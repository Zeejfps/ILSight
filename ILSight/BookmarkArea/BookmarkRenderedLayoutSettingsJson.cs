namespace ILSight.BookmarkArea;

/// <summary>ILSpy's display settings a bookmark was made in, as a settings or bookmarks file can hold them.</summary>
internal sealed record BookmarkRenderedLayoutSettingsJson
{
    public bool FoldBraces { get; init; }

    public bool ExpandXmlDocumentationComments { get; init; }

    public bool ExpandMemberDefinitions { get; init; }

    public bool ExpandUsingDeclarations { get; init; }

    public bool ShowDebugInfo { get; init; }

    public bool IndentationUseTabs { get; init; }

    public int IndentationSize { get; init; }

    public int IndentationTabSize { get; init; }
}
