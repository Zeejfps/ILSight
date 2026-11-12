namespace ILSight.BookmarkArea;

/// <summary>ILSpy's display settings that shaped the text the bookmark was made in.</summary>
public sealed record BookmarkRenderedLayoutSettings
{
    public required bool FoldBraces { get; init; }

    public required bool ExpandXmlDocumentationComments { get; init; }

    public required bool ExpandMemberDefinitions { get; init; }

    public required bool ExpandUsingDeclarations { get; init; }

    public required bool ShowDebugInfo { get; init; }

    public required bool IndentationUseTabs { get; init; }

    public required int IndentationSize { get; init; }

    public required int IndentationTabSize { get; init; }
}
