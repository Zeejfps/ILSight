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
                revision += DocumentPanels.CountViewChanges(view);
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

    /// <summary>Lets each view follow the grid or picture its tab shows.</summary>
    public void FollowViewContent()
    {
        foreach (var (tab, view) in _views)
        {
            view.Picture.Follow(tab.Image);
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
    /// following what the tab shows from the start, so what is asked of the view before it is next drawn,
    /// such as a row to reveal, is not undone when it first takes the tab's content.</summary>
    public void AddView(DocumentTab tab, string panel)
    {
        if (_views.ContainsKey(tab))
        {
            return;
        }

        var view = new TabView { Panel = panel, ImageId = $"{panel}-image" };
        view.Picture.Follow(tab.Image);
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
        _droppedChanges += DocumentPanels.CountViewChanges(view);
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
}
