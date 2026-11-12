
namespace ILSight.DisplayOptionArea;

/// <summary>How the app is drawn, as a settings file can hold it. What it lacks, or holds as null, is
/// as <see cref="DisplayOptions.Default"/> has it; a font size out of range is brought within it and the
/// interface scale is the nearest offered.</summary>
internal sealed record DisplayOptionsJson(
    float? FontSize,
    bool? ShowLineNumbers,
    bool? HighlightCurrentLine,
    bool? HighlightMatchingBrackets,
    bool? HighlightUsagesAtCaret,
    bool? ShowOmnibar,
    float? UiScale,
    CodeFontJson? Font,
    TokenFormat? TreeTokens,
    bool? ShowEmptyMetadataTables,
    bool? NestedNamespaces,
    bool? SystemTitleBar,
    bool? PreviewTabs);
