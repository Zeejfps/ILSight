using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>How the app is drawn: the scale of the whole interface, 1 for 100%, and whether the window
/// keeps the system's title bar, with the menus in a bar of their own, rather than the menus drawn in
/// the title bar where the platform allows; how the documents draw code: its font, null for the bundled
/// one, its size, null for the theme's, the aids drawn around it, among them every use of the symbol
/// at the caret, and whether an omnibar over each shows the path to what it shows and searches; and how
/// the tree writes metadata tokens, whether it lists metadata tables with no rows and whether it nests each namespace in the
/// one its name extends; and whether what is picked shows in a preview tab.</summary>
public sealed record DisplayOptions(
    CodeFontSize? FontSize = null,
    bool ShowLineNumbers = true,
    bool HighlightCurrentLine = false,
    bool HighlightMatchingBrackets = true,
    bool HighlightUsagesAtCaret = false,
    bool ShowOmnibar = true,
    float UiScale = 1f,
    CodeFont? Font = null,
    TokenFormat TreeTokens = TokenFormat.Hidden,
    bool ShowEmptyMetadataTables = false,
    bool NestedNamespaces = false,
    bool SystemTitleBar = false,
    bool PreviewTabs = true)
{

    private static readonly float[] Scales = [0.75f, 0.8f, 0.9f, 1f, 1.1f, 1.25f, 1.5f, 1.75f, 2f];

    public static DisplayOptions Default { get; } = new();

    /// <summary>The interface scales offered, smallest first.</summary>
    public static IReadOnlyList<float> UiScales => Scales;
}
