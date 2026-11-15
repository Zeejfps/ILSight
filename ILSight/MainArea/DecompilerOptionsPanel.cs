namespace ILSight.MainArea;

/// <summary>The decompiler options panel's own state: what it is searched for. Its owner is the panel, its lifetime the
/// screen's (not saved). The panel is shown in one place, a dock panel, so it is held once (<see cref="UiState"/>); its
/// places would be independent: were the panel shown in a second place, each would hold its own, a search typed in one
/// not filtering the other.</summary>
public sealed class DecompilerOptionsPanel
{
    /// <summary>What the panel is searched for, which its search box is bound to; empty shows every option.</summary>
    public string Query = "";
}
