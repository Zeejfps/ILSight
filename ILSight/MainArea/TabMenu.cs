namespace ILSight.MainArea;

/// <summary>The context menu of a document's tab: the document it was opened on. Its owner is the menu, its lifetime
/// from one opening of the menu to the next. One menu is open at a time, whichever tab it was opened from, so every
/// tab's menu is the one place, kept in step: held once (<see cref="UiState"/>), each opening retargeting it.</summary>
public sealed class TabMenu
{
    /// <summary>The document whose tab's context menu was opened, or null.</summary>
    public DocumentTab? Target { get; private set; }

    /// <summary>Opens the menu's actions on <paramref name="tab"/>.</summary>
    public void TargetMenu(DocumentTab tab)
    {
        Target = tab;
    }
}
