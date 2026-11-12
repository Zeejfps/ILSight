namespace ILSight.DocumentViewing;

/// <summary>How each open tab is shown, found by the tab or by the panel it is docked as: each tab with a
/// view has a panel of its own, and a new panel is never one a saved document was docked as. The images of
/// the views dropped wait here until the canvas frees them.</summary>
public sealed class DocumentViews
{
    public readonly Dictionary<DocumentTab, TabView> Views = new();

    /// <summary>The buffer <see cref="DocumentPanels.ListClosedTabs"/> reuses.</summary>
    public readonly List<DocumentTab> Closed = new();

    /// <summary>The images of the views dropped, until the canvas frees them.</summary>
    public readonly List<string> ReleasedImages = new();

    /// <summary>The panels saved documents were docked as, which no new tab gets.</summary>
    public readonly HashSet<string> Skipped = new();

    /// <summary>Counts the panels handed out, which number the next.</summary>
    public int Panels;

    /// <summary>Counts the views made and dropped.</summary>
    public int OwnChanges;

    /// <summary>What the views dropped had counted, as <see cref="Revision"/> does.</summary>
    public int DroppedChanges;

    /// <summary>Counts up whenever a view is made or dropped, or a view's diagram or comparison is viewed
    /// otherwise as <see cref="DiagramViewState.Changes"/> and <see cref="ComparisonRows.Changes"/> count; it
    /// never counts down. Allocates nothing.</summary>
    public int Revision
    {
        get
        {
            var revision = OwnChanges + DroppedChanges;
            foreach (var view in Views.Values)
            {
                revision += DocumentPanels.CountViewChanges(view);
            }

            return revision;
        }
    }
}
