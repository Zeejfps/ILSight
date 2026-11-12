
namespace ILSight;

/// <summary>What is done to the open tabs: closing them and choosing among them. Which document each is changes
/// through <see cref="TabDocument"/>, and its load through <see cref="TabLoad"/>.</summary>
public static class Tabs
{
    /// <summary>Cancels the tab's work and closes it. The tab stops being active if it was.</summary>
    public static void Remove(DocumentTabs tabs, DocumentTab tab)
    {
        if (!tabs.Open.TryRemove(tab))
        {
            return;
        }

        tabs.GetDocument(tab).Close();
        tabs.ClosedChanges += tab.Changes;
        if (tab == tabs.Active)
        {
            tabs.LastWorkedIn.Value = null;
        }

        if (tab == tabs.Preview)
        {
            tabs.Previewing.Value = null;
        }

        if (tab == tabs.LastDiagram)
        {
            tabs.LastDiagram = null;
        }
    }

    /// <summary>Makes <paramref name="tab"/> the active tab, and the diagram tab when it shows a diagram.</summary>
    public static void MakeActive(DocumentTabs tabs, DocumentTab tab)
    {
        if (!tabs.Open.Contains(tab))
        {
            throw new InvalidOperationException("Only an open tab can be active.");
        }

        tabs.LastWorkedIn.Value = tab;
        if (tab.Diagram is not null)
        {
            tabs.LastDiagram = tab;
        }
    }

    /// <summary>Makes <paramref name="tab"/> the preview tab; the one before it stays open, kept.</summary>
    public static void MakePreview(DocumentTabs tabs, DocumentTab tab)
    {
        if (!tabs.Open.Contains(tab))
        {
            throw new InvalidOperationException("Only an open tab can be the preview tab.");
        }

        tabs.Previewing.Value = tab;
    }

    /// <summary>Keeps <paramref name="tab"/> open: it is no longer the preview tab, if it was.</summary>
    public static void Keep(DocumentTabs tabs, DocumentTab tab)
    {
        if (tab != tabs.Preview)
        {
            return;
        }

        tabs.Previewing.Value = null;
    }

    public static bool IsOpen(DocumentTabs tabs, DocumentTab tab)
    {
        return tabs.Open.Contains(tab);
    }

    /// <summary><paramref name="tab"/> if it is still open, else null.</summary>
    public static DocumentTab? KeepIfOpen(DocumentTabs tabs, DocumentTab? tab)
    {
        if (tab is null)
        {
            return null;
        }

        if (!tabs.Open.Contains(tab))
        {
            return null;
        }

        return tab;
    }
}
