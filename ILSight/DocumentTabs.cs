namespace ILSight;

/// <summary>The open document tabs, the one the user last worked in, the preview tab, which shows
/// what is picked until it is kept open, and the diagram the user last worked in; each of those is always
/// one of the open tabs, which <see cref="Tabs"/> sees to: change these only through it.</summary>
public sealed class DocumentTabs
{
    /// <summary>The tabs open, in order.</summary>
    public readonly List<DocumentTab> Open = new();

    /// <summary>The tab of the diagram last worked in, which may since show something else.</summary>
    public DocumentTab? LastDiagram;

    /// <summary>The tabs opened and closed and the changes of which is active or the preview.</summary>
    public int OwnChanges;

    /// <summary>What the tabs closed had counted as <see cref="DocumentTab.Changes"/>.</summary>
    public int ClosedChanges;

    /// <summary>The tab the user last worked in, where the tree and navigation show what they pick.</summary>
    public DocumentTab? Active;

    /// <summary>The tab that shows what is picked, replaced by what is picked next, until it is kept
    /// open; null when no tab is one.</summary>
    public DocumentTab? Preview;

    /// <summary>The tab of the diagram the user last worked in, which types shown in a diagram are added
    /// to; null when no open tab shows a diagram the user worked in.</summary>
    public DocumentTab? Diagram
    {
        get
        {
            if (LastDiagram == null)
                return null;
            if (LastDiagram.Diagram == null)
                return null;
            return LastDiagram;
        }
    }

    /// <summary>Counts up whenever a tab opens or closes, another becomes the active or the preview tab,
    /// or an open one changes as <see cref="DocumentTab.Changes"/> counts; it never counts down.
    /// Allocates nothing.</summary>
    public int Changes
    {
        get
        {
            var changes = OwnChanges + ClosedChanges;
            foreach (var tab in Open)
                changes += tab.Changes;
            return changes;
        }
    }
}
