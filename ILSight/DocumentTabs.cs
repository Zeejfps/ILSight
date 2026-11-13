using System.Runtime.CompilerServices;
using ILSight.Core;

namespace ILSight;

/// <summary>The open document tabs, the one the user last worked in, the preview tab, which shows
/// what is picked until it is kept open, and the diagram the user last worked in; each of those is always
/// one of the open tabs, which its methods see to: closing tabs and choosing among them; and which document each
/// tab is (<see cref="GetDocument"/>), which only their holder, <see cref="Navigation"/>, changes, through
/// <see cref="TabDocument"/>, and its load through <see cref="TabLoad"/>.</summary>
public sealed class DocumentTabs
{
    /// <summary>The id the last tab opened was given.</summary>
    private int _lastTabId;

    /// <summary>Which document each tab opened here is, kept as long as the tab is.</summary>
    private readonly ConditionalWeakTable<DocumentTab, TabDocument> _documents = new();

    /// <summary>Opens a new tab, showing nothing yet, with an id no tab had before.</summary>
    public DocumentTab AddNew()
    {
        _lastTabId++;
        var document = new TabDocument();
        var tab = new DocumentTab(_lastTabId, document);
        _documents.Add(tab, document);
        Open.Add(tab);
        return tab;
    }

    /// <summary>Which document <paramref name="tab"/>, opened here, is, to change it.</summary>
    public TabDocument GetDocument(DocumentTab tab)
    {
        if (!_documents.TryGetValue(tab, out var document))
        {
            throw new InvalidOperationException($"Tab {tab.Id} was not opened among these tabs.");
        }

        return document;
    }

    /// <summary>The tabs open, in order.</summary>
    public readonly TrackedList<DocumentTab> Open = new();

    /// <summary>The tab of the diagram last worked in, which may since show something else.</summary>
    private DocumentTab? _lastDiagram;

    /// <summary>What the tabs closed had counted as <see cref="DocumentTab.Changes"/>.</summary>
    private int _closedChanges;

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
            var lastDiagram = _lastDiagram;
            if (lastDiagram is null)
            {
                return null;
            }

            if (lastDiagram.Diagram is null)
            {
                return null;
            }

            return lastDiagram;
        }
    }

    /// <summary>Counts up whenever a tab opens or closes, another becomes the active or the preview tab,
    /// or an open one changes as <see cref="DocumentTab.Changes"/> counts; it never counts down.
    /// Allocates nothing.</summary>
    public int Changes
    {
        get
        {
            var changes = Open.Revision + LastWorkedIn.Version + Previewing.Version + _closedChanges;
            foreach (var tab in Open)
            {
                changes += tab.Changes;
            }

            return changes;
        }
    }

    /// <summary>Cancels the tab's work and closes it. The tab stops being active if it was.</summary>
    public void Remove(DocumentTab tab)
    {
        if (!Open.TryRemove(tab))
        {
            return;
        }

        GetDocument(tab).Close();
        _closedChanges += tab.Changes;
        if (tab == Active)
        {
            LastWorkedIn.Value = null;
        }

        if (tab == Preview)
        {
            Previewing.Value = null;
        }

        if (tab == _lastDiagram)
        {
            _lastDiagram = null;
        }
    }

    /// <summary>Makes <paramref name="tab"/> the active tab, and the diagram tab when it shows a diagram.</summary>
    public void MakeActive(DocumentTab tab)
    {
        if (!Open.Contains(tab))
        {
            throw new InvalidOperationException("Only an open tab can be active.");
        }

        LastWorkedIn.Value = tab;
        if (tab.Diagram is not null)
        {
            _lastDiagram = tab;
        }
    }

    /// <summary>Makes <paramref name="tab"/> the preview tab; the one before it stays open, kept.</summary>
    public void MakePreview(DocumentTab tab)
    {
        if (!Open.Contains(tab))
        {
            throw new InvalidOperationException("Only an open tab can be the preview tab.");
        }

        Previewing.Value = tab;
    }

    /// <summary>Keeps <paramref name="tab"/> open: it is no longer the preview tab, if it was.</summary>
    public void Keep(DocumentTab tab)
    {
        if (tab != Preview)
        {
            return;
        }

        Previewing.Value = null;
    }

    public bool IsOpen(DocumentTab tab)
    {
        return Open.Contains(tab);
    }

    /// <summary><paramref name="tab"/> if it is still open, else null.</summary>
    public DocumentTab? KeepIfOpen(DocumentTab? tab)
    {
        if (tab is null)
        {
            return null;
        }

        if (!Open.Contains(tab))
        {
            return null;
        }

        return tab;
    }
}
