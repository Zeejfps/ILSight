using ILSight.Core;

namespace ILSight;

/// <summary>The open document tabs, the one the user last worked in, the preview tab, which shows
/// what is picked until it is kept open, and the diagram the user last worked in; each of those is always
/// one of the open tabs, which <see cref="Tabs"/> sees to: change these only through it.</summary>
public sealed class DocumentTabs
{
    /// <summary>The id the last tab opened was given.</summary>
    private int _lastTabId;

    /// <summary>Opens a new tab, showing nothing yet, with an id no tab had before.</summary>
    public DocumentTab AddNew()
    {
        _lastTabId++;
        var tab = new DocumentTab { Id = _lastTabId };
        Open.Add(tab);
        return tab;
    }

    /// <summary>The tabs open, in order.</summary>
    public readonly TrackedList<DocumentTab> Open = new();

    /// <summary>The tab of the diagram last worked in, which may since show something else.</summary>
    public DocumentTab? LastDiagram;

    /// <summary>What the tabs closed had counted as <see cref="DocumentTab.Changes"/>.</summary>
    public int ClosedChanges;

    /// <summary>The tab the user last worked in, and how many times that changed.</summary>
    public Tracked<DocumentTab?> LastWorkedIn { get; } = new(null);

    /// <summary>The tab that shows what is picked, and how many times that changed.</summary>
    public Tracked<DocumentTab?> Previewing { get; } = new(null);

    /// <summary>The tab the user last worked in, where the tree and navigation show what they pick.</summary>
    public DocumentTab? Active => LastWorkedIn.Value;

    /// <summary>The tab that shows what is picked, replaced by what is picked next, until it is kept
    /// open; null when no tab is one.</summary>
    public DocumentTab? Preview => Previewing.Value;

    /// <summary>The tab of the diagram the user last worked in, which types shown in a diagram are added
    /// to; null when no open tab shows a diagram the user worked in.</summary>
    public DocumentTab? Diagram
    {
        get
        {
            if (LastDiagram is null)
            {
                return null;
            }

            if (LastDiagram.Diagram is null)
            {
                return null;
            }

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
            var changes = Open.Revision + LastWorkedIn.Version + Previewing.Version + ClosedChanges;
            foreach (var tab in Open)
            {
                changes += tab.Changes;
            }

            return changes;
        }
    }
}
