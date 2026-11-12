
namespace ILSight;

/// <summary>How the app is drawn, as a settings file can hold it. What it lacks, or holds as null, is
/// as <see cref="DisplayOptions.Default"/> has it; a font size out of range is brought within it and the
/// interface scale is the nearest offered.</summary>
internal sealed record DisplayOptionsJson
{
    public float? FontSize { get; init; }

    public bool? ShowLineNumbers { get; init; }

    public bool? HighlightCurrentLine { get; init; }

    public bool? HighlightMatchingBrackets { get; init; }

    public bool? HighlightUsagesAtCaret { get; init; }

    public bool? ShowOmnibar { get; init; }

    public float? UiScale { get; init; }

    public CodeFontJson? Font { get; init; }

    public TokenFormat? TreeTokens { get; init; }

    public bool? ShowEmptyMetadataTables { get; init; }

    public bool? NestedNamespaces { get; init; }

    public bool? SystemTitleBar { get; init; }

    public bool? PreviewTabs { get; init; }
}
