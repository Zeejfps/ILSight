using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.DiagramArea;

namespace ILSight.DocumentViewArea;

/// <summary>How each open tab is shown, found by the tab or by the panel it is docked as: each tab with a
/// view has a panel of its own, and a new panel is never one a saved document was docked as. The images of
/// the views dropped wait here until the canvas frees them.</summary>
public sealed class DocumentViews
{
    private const string PanelPrefix = "document-";

    private readonly Dictionary<DocumentTab, TabView> _views = new();
    private readonly List<DocumentTab> _closed = new();
    private readonly List<string> _releasedImages = new();
    private readonly HashSet<string> _skipped = new();

    private int _panelsHandedOut;

    private int _ownChanges;

    // What the views dropped had counted, so that Revision never counts down when one goes.
    private int _droppedChanges;

    public IReadOnlyDictionary<DocumentTab, TabView> Views => _views;

    /// <summary>How tall the tab area a document was last drawn in was as the screen laid it out; the window's height
    /// while it had not been laid out yet; null before a document was drawn.</summary>
    public float? TabAreaHeight { get; private set; }

    /// <summary>Counts up whenever a view is made or dropped, or a view's diagram or comparison is viewed
    /// otherwise as <see cref="DiagramViewState.Changes"/> and <see cref="ComparisonRows.Changes"/> count; it
    /// never counts down. Allocates nothing.</summary>
    public int Revision
    {
        get
        {
            var revision = _ownChanges + _droppedChanges;
            foreach (var view in _views.Values)
            {
                revision += view.Changes;
            }

            return revision;
        }
    }

    /// <summary>The open tab docked as <paramref name="panel"/>. Allocates nothing.</summary>
    public DocumentTab? FindTab(string? panel)
    {
        foreach (var (tab, view) in _views)
        {
            if (view.Panel == panel)
            {
                return tab;
            }
        }
        return null;
    }

    /// <summary>Has each view's picture take the image its tab shows, reset when that image changed.</summary>
    public void ResetPicturesWhenImageChanges()
    {
        foreach (var (tab, view) in _views)
        {
            view.Picture.ResetWhenImageChanges(tab.Image);
        }
    }

    /// <summary>Takes <paramref name="height"/> as how tall the tab area a document is drawn in is now.</summary>
    public void RecordTabAreaHeight(float height)
    {
        TabAreaHeight = height;
    }

    /// <summary>A document panel no tab was shown in before, nor any of those <see cref="SkipPanels"/> was given.</summary>
    public string CreatePanel()
    {
        var panel = NextPanel();
        while (_skipped.Contains(panel))
        {
            panel = NextPanel();
        }

        return panel;
    }

    private string NextPanel()
    {
        _panelsHandedOut++;
        return $"{PanelPrefix}{_panelsHandedOut}";
    }

    /// <summary>Leaves <paramref name="panels"/>, which saved documents were docked as, to them:
    /// <see cref="CreatePanel"/> hands out none of them.</summary>
    public void SkipPanels(IEnumerable<string> panels)
    {
        foreach (var panel in panels)
        {
            _skipped.Add(panel);
        }
    }

    /// <summary>Shows <paramref name="tab"/> in a new panel, unless it is shown already.</summary>
    public void AddView(DocumentTab tab)
    {
        if (_views.ContainsKey(tab))
        {
            return;
        }

        AddView(tab, CreatePanel());
    }

    /// <summary>Shows <paramref name="tab"/> in <paramref name="panel"/>, unless it is shown already, its view
    /// taking what the tab shows from the start, so what is asked of the view before it is next drawn,
    /// such as a row to reveal, is not undone when it first takes the tab's content.</summary>
    public void AddView(DocumentTab tab, string panel)
    {
        if (_views.ContainsKey(tab))
        {
            return;
        }

        var view = new TabView { Panel = panel, ImageId = $"{panel}-image" };
        view.Picture.ResetWhenImageChanges(tab.Image);
        _views.Add(tab, view);
        _ownChanges++;
    }

    /// <summary>Drops the view of <paramref name="tab"/>, keeping its image to free.</summary>
    public void RemoveView(DocumentTab tab)
    {
        if (!_views.Remove(tab, out var view))
        {
            return;
        }

        _releasedImages.Add(view.ImageId);
        _droppedChanges += view.Changes;
        _ownChanges++;
    }

    /// <summary>The tabs with a view that <paramref name="navigation"/> no longer holds, in a list the next call reuses.</summary>
    public IReadOnlyList<DocumentTab> ListClosedTabs(Navigation navigation)
    {
        _closed.Clear();
        foreach (var tab in _views.Keys)
        {
            if (!navigation.IsOpen(tab))
            {
                _closed.Add(tab);
            }
        }
        return _closed;
    }

    /// <summary>The images of the views dropped since the last call, for the canvas to free.</summary>
    public IReadOnlyList<string> TakeReleasedImages()
    {
        if (_releasedImages.Count == 0)
        {
            return Array.Empty<string>();
        }

        var released = _releasedImages.ToArray();
        _releasedImages.Clear();
        return released;
    }

    /// <summary>How <paramref name="tab"/>, an open tab, is shown.</summary>
    public TabView GetView(DocumentTab tab)
    {
        return _views[tab];
    }

    /// <summary>The panel <paramref name="tab"/>, an open tab, is docked as.</summary>
    public string GetPanel(DocumentTab tab)
    {
        return GetView(tab).Panel;
    }

    public bool HasView(DocumentTab tab)
    {
        return _views.ContainsKey(tab);
    }

    /// <summary>Whether a tab with a view is docked as <paramref name="panel"/>.</summary>
    public bool IsTabPanel(string panel)
    {
        return FindTab(panel) is not null;
    }

    /// <summary>The panel <paramref name="tab"/> is docked as, or null when there is no tab or it has no view.</summary>
    public string? FindPanel(DocumentTab? tab)
    {
        if (tab is null)
        {
            return null;
        }

        if (!_views.TryGetValue(tab, out var view))
        {
            return null;
        }

        return view.Panel;
    }

    /// <summary>The view of <paramref name="tab"/>, or null when it has none.</summary>
    public TabView? FindView(DocumentTab tab)
    {
        return _views.GetValueOrDefault(tab);
    }

    /// <summary>Has each tab open in <paramref name="navigation"/> keep where its view stands, as the frame drew it.</summary>
    public void KeepTabPositions(Navigation navigation)
    {
        var open = navigation.OpenTabs;
        for (var i = 0; i < open.Count; i++)
        {
            KeepTabPosition(navigation, open[i]);
        }
    }

    private void KeepTabPosition(Navigation navigation, DocumentTab tab)
    {
        var view = FindView(tab);
        if (view is null)
        {
            return;
        }

        var shown = view.Shown;
        if (shown is null)
        {
            return;
        }

        if (shown.IsShownBy(tab))
        {
            tab.Load.KeepPosition(view.GetPosition(tab));
            return;
        }

        // The tab moved on while the frame was drawn: the history entry left from it keeps the position.
        navigation.KeepPositionLeft(shown, view.GetShownPosition());
    }

    /// <summary>The panel <paramref name="tab"/> is docked as, and how its diagram or comparison is viewed, as the
    /// settings keep it (<see cref="SavePanels"/>); null when it has no view.</summary>
    public TabPanel? FindTabPanel(DocumentTab tab)
    {
        if (!_views.TryGetValue(tab, out var view))
        {
            return null;
        }

        return new TabPanel { Tab = tab, Panel = view.Panel, View = ToPanelView(tab, view) };
    }

    /// <summary>The panel each tab with a view is docked as, and how its diagram or comparison is viewed,
    /// for the settings to keep.</summary>
    public List<TabPanel> SavePanels()
    {
        var panels = new List<TabPanel>();
        foreach (var (tab, view) in _views)
        {
            panels.Add(new TabPanel { Tab = tab, Panel = view.Panel, View = ToPanelView(tab, view) });
        }

        return panels;
    }

    private static TabPanelView ToPanelView(DocumentTab tab, TabView view)
    {
        if (tab.Content is TabContent.Diagram)
        {
            return new TabPanelView.Diagram(Diagrams.ToViewRecord(view.Diagram));
        }

        if (tab.Content is TabContent.Comparison)
        {
            return new TabPanelView.Comparison(view.Comparison.ToViewRecord());
        }

        return new TabPanelView.None();
    }

    /// <summary>Shows the reopened tab in the panel it was docked as, or in a new one when another tab is shown in
    /// one by that name, its diagram's or comparison's view returned to how it was saved; returns the panel.</summary>
    public string AddReopenedView(ReopenedTab reopened)
    {
        var panel = ChooseReopenedPanel(reopened.Record.Panel);
        AddView(reopened.Tab, panel);
        RestoreDiagramView(reopened);
        RestoreComparisonView(reopened);
        return panel;
    }

    private string ChooseReopenedPanel(string saved)
    {
        if (!IsTabPanel(saved))
        {
            return saved;
        }

        return CreatePanel();
    }

    private void RestoreDiagramView(ReopenedTab reopened)
    {
        if (reopened.Record is not DiagramDocumentRecord diagram)
        {
            return;
        }

        var saved = diagram.View;
        if (saved is null)
        {
            return;
        }

        FindView(reopened.Tab)?.Diagram.RestoreView(saved);
    }

    private void RestoreComparisonView(ReopenedTab reopened)
    {
        if (reopened.Record is not ComparisonDocumentRecord comparison)
        {
            return;
        }

        var saved = comparison.View;
        if (saved is null)
        {
            return;
        }

        FindView(reopened.Tab)?.Comparison.RestoreView(saved);
    }

    /// <summary>Returns the view of <paramref name="tab"/>, an open tab, to <paramref name="position"/>, when it is
    /// a position of the view the tab shows, a grid's or a code view's.</summary>
    public void RestoreView(DocumentTab tab, ViewPosition position)
    {
        var view = _views.GetValueOrDefault(tab);
        if (view is null)
        {
            return;
        }

        view.RecordShown(tab);
        var shown = tab.Grid;
        if (shown is null)
        {
            var code = position.Code;
            if (code is not null)
            {
                CodeViewWidget.RestorePosition(view.Code, code.Value);
            }

            return;
        }
        var grid = position.Grid;
        if (grid is not null)
        {
            view.GetGridState(shown).RestorePosition(grid.Value);
        }
    }

    /// <summary>Scrolls the code of <paramref name="tab"/>, an open tab, to <paramref name="line"/> and flashes it.</summary>
    public void RevealLine(DocumentTab tab, int line)
    {
        var view = _views.GetValueOrDefault(tab);
        if (view is null)
        {
            return;
        }

        if (line >= tab.Document.Lines.Length)
        {
            return;
        }

        view.RecordShown(tab);
        CodeViewWidget.RevealLine(view.Code, line);
    }

    /// <summary>Selects the grid row <paramref name="row"/> of <paramref name="tab"/>, an open tab, and scrolls to it.</summary>
    public void RevealRow(DocumentTab tab, int row)
    {
        var view = _views.GetValueOrDefault(tab);
        if (view is null)
        {
            return;
        }

        var grid = tab.Grid;
        if (grid is null || row >= grid.RowCount)
        {
            return;
        }

        view.RecordShown(tab);
        view.GetGridState(grid).RevealRow(row);
    }

    /// <summary>Selects the card of <paramref name="type"/> on the diagram <paramref name="tab"/>, an open tab,
    /// shows, and brings it into view once it is laid out.</summary>
    public void FocusCard(DocumentTab tab, CodeReference.Entity type)
    {
        var view = _views.GetValueOrDefault(tab);
        if (view is null)
        {
            return;
        }

        view.Diagram.FocusCard(type);
    }
}
