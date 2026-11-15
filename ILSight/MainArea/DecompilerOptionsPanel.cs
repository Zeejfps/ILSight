namespace ILSight.MainArea;

/// <summary>The decompiler options panel's own state: what it is searched for, and which of its groups are folded. Its
/// owner is the panel; the search lives as long as the screen (not saved), the folds as long as the settings (the
/// settings' save reads them here, and the screen is made with them restored). The panel is shown in one place, a dock
/// panel, so it is held once (<see cref="UiState"/>); its places would be independent: were the panel shown in a second
/// place, each would hold its own, a search typed or a group folded in one leaving the other as it was.</summary>
public sealed class DecompilerOptionsPanel
{
    /// <summary>What the panel is searched for, which its search box is bound to; empty shows every option.</summary>
    public string Query = "";

    /// <summary>The groups of options folded in the panel, by key.</summary>
    public required FoldedGroups Folded { get; init; }
}
