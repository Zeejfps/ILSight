namespace ILSight.MainArea;

/// <summary>The context menu of a document's tab: the document it was opened on.</summary>
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
