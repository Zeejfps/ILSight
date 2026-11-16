using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.MainArea;
using ILSight.SearchArea;

namespace ILSight;

/// <summary>The views as the user opens them in the layout: docked where each goes beside the active document and
/// brought to the front, the search panel's box then taking the focus, so what is typed next goes into it; and the
/// layout put back as it starts out around the open documents. It holds the layout privately, as the parts this rule
/// spans: it hands it to the framework's dock space itself (<see cref="DrawDockSpace{TState}"/>, which also docks,
/// floats and closes on the user's drag), the settings a copy of the arrangement, and what else reads it the answers
/// it needs; the tabs' panels are docked, brought to the front and closed through it. What the dock space writes the
/// rule follows: a panel the layout makes for itself opens a tab, and a closed panel's tab closes with it, the search
/// cleared with its panel.</summary>
public sealed class Panels
{
    private readonly MainLayout _layout;
    private readonly IDockWidget _dockWidget;
    private readonly DocumentViews _documents;
    private readonly TabFocus _tabFocus;
    private readonly Search _search;
    private readonly IAppCommandWriter _commands;

    public Panels(MainLayout layout, IDockWidget dockWidget, DocumentViews documents, TabFocus tabFocus, Search search, IAppCommandWriter commands)
    {
        _layout = layout;
        _dockWidget = dockWidget;
        _documents = documents;
        _tabFocus = tabFocus;
        _search = search;
        _commands = commands;
        var dock = layout.Dock;
        dock.NewPanel = OpenTabForPanel;
        dock.PanelClosed += OnPanelClosed;
    }

    /// <summary>Docks <paramref name="view"/> if it is not, by the active document where it goes there, and
    /// brings it to the front; the search panel as <see cref="OpenSearch"/> does.</summary>
    public void Open(DockView view)
    {
        if (view == DockView.Search)
        {
            OpenSearch();
            return;
        }

        Docking.Open(_layout, view, ActivePanel);
    }

    /// <summary>Docks the search panel under the documents if it is closed, brings it to the front
    /// and puts the caret in its search box.</summary>
    public void OpenSearch()
    {
        Docking.Open(_layout, DockView.Search, ActivePanel);
        _search.AskForFocus();
    }

    /// <summary>The panel of the active tab, which the views dock beside, or null.</summary>
    public string? ActivePanel => _documents.FindPanel(_tabFocus.Active);

    /// <summary>Whether <paramref name="panel"/> is in the layout, docked or floating.</summary>
    public bool IsOpen(string panel)
    {
        return _layout.Dock.Contains(panel);
    }

    /// <summary>Whether any panel is in the layout.</summary>
    public bool HasPanels => _layout.Dock.HasPanels();

    /// <summary>How many times the layout changed, for the settings to tell when to take its arrangement again.</summary>
    public int LayoutChanges => _layout.Dock.Changes;

    /// <summary>A copy of how the panels are arranged, for the settings to keep.</summary>
    public DockLayout CopyArrangement()
    {
        return _layout.Dock.CopyArrangement();
    }

    /// <summary>The documents' panels in the layout, docked or floating.</summary>
    public List<string> ListDocumentPanels()
    {
        return Docking.ListDocumentPanels(_layout);
    }

    /// <summary>Draws the framework's dock space over the layout, through the dock widget the root made, which docks,
    /// floats and closes panels itself on the user's drag or a close button; the caller draws each panel it hands out.</summary>
    public DockSpace DrawDockSpace<TState>(Ui ui, string id, TState state, Func<TState, string, DockTab> tab, string tabListTooltip)
    {
        return _dockWidget.Draw(ui, id, _layout.Dock, state, tab, tabListTooltip);
    }

    /// <summary>Puts the layout back as it starts out: the assemblies left of every open document,
    /// tabbed together in one group, an empty tab asked for when there is none. Floating windows and the other views
    /// close; the documents stay open.</summary>
    public void Reset()
    {
        var shown = Docking.ListDocumentPanels(_layout);
        if (shown.Count == 0)
        {
            shown.Add(OpenActiveTabPanel());
        }

        Docking.ResetLayout(_layout, shown);
        ActivateActivePanel();
    }

    private string OpenActiveTabPanel()
    {
        var panel = _documents.CreatePanel();
        Commands.Later(_commands, (panels: this, panel),
            static (a, s) => QueuedCommands.OpenTabIn(s.panels, a, s.panel, focus: true));
        return panel;
    }

    /// <summary>Opens an empty tab shown in <paramref name="panel"/>, unless the panel was closed meanwhile; returns it
    /// when it is to become the active tab, as asked or because the layout focuses its panel, else null.</summary>
    public DocumentTab? OpenTabIn(Navigation navigation, string panel, bool focus)
    {
        if (!_layout.Dock.Contains(panel))
        {
            return null;
        }

        var tab = navigation.OpenEmptyTab();
        _documents.AddView(tab, panel);
        var becomesActive = focus || _layout.Dock.Focused == panel;
        if (!becomesActive)
        {
            return null;
        }

        return tab;
    }

    // A new panel the layout makes for itself, with an empty tab asked for to be opened in it.
    private string OpenTabForPanel()
    {
        var panel = _documents.CreatePanel();
        Commands.Later(_commands, (panels: this, panel), static (a, s) => QueuedCommands.OpenTabIn(s.panels, a, s.panel, focus: false));
        return panel;
    }

    // Called when a panel is closed: clears the search with its panel, and asks for the tab a closed document was to
    // be closed.
    private void OnPanelClosed(string panel)
    {
        if (panel == DockPanels.Search)
        {
            _search.Clear();
        }

        CloseTabOf(panel);
    }

    private void CloseTabOf(string panel)
    {
        var tab = _documents.FindTab(panel);
        if (tab is null)
        {
            return;
        }

        _documents.RemoveView(tab);
        Commands.Later(_commands, (panels: this, tab), static (a, s) => QueuedCommands.CloseTabAndFocusNext(s.panels, a, s.tab));
    }

    /// <summary>Shows the reopened <paramref name="tabs"/> in the panels they were docked as: in place where
    /// the layout still has one no tab is shown in, else, renamed when another tab is shown in one by that
    /// name, arranged as <paramref name="saved"/>, the documents' saved arrangement, has them, beside the
    /// documents; returns their diagrams' views to how they were saved, and brings the active tab to the front.</summary>
    public void ShowReopenedTabs(IReadOnlyList<ReopenedTab> tabs, DockLayout? saved)
    {
        var panels = new Dictionary<string, string>();
        foreach (var reopened in tabs)
        {
            panels[reopened.Record.Panel] = _documents.AddReopenedView(reopened);
        }

        DockUndocked(panels, saved);
        ActivateActivePanel();
    }

    private void DockUndocked(Dictionary<string, string> panels, DockLayout? saved)
    {
        var undocked = panels.Values
            .Where(p => !_layout.Dock.Contains(p))
            .ToList();
        if (undocked.Count == 0)
        {
            return;
        }

        var arrangement = ArrangeUndocked(undocked, panels, saved);
        Docking.DockArranged(_layout, undocked, arrangement);
    }

    private static DockLayout ArrangeUndocked(List<string> undocked, Dictionary<string, string> panels, DockLayout? layout)
    {
        if (layout is null)
        {
            return ArrangeInOneGroup(undocked);
        }

        var arrangement = layout.CopyArrangement();
        DockPanels.Rename(arrangement, panels);
        arrangement.Retain(undocked.Contains);
        return arrangement;
    }

    private static DockLayout ArrangeInOneGroup(List<string> panels)
    {
        var group = DockLayout.Group(panels.ToArray());
        return new DockLayout { Root = group };
    }

    // Brings the active tab's panel to the front and focuses it, when the layout has it.
    private void ActivateActivePanel()
    {
        var panel = ActivePanel;
        if (panel is null)
        {
            return;
        }

        if (!_layout.Dock.Contains(panel))
        {
            return;
        }

        _layout.Dock.Activate(panel);
    }

    /// <summary>Shows each open tab no panel was given, such as the one a run starts with when the open
    /// list kept no documents, in the place of the documents the layout was restored with, then takes the
    /// panels of the documents no tab is shown in out of the layout.</summary>
    public void ShowInRestoredDocuments(Navigation navigation)
    {
        for (var i = 0; i < navigation.OpenTabs.Count; i++)
        {
            ShowInRestoredPlace(navigation.OpenTabs[i]);
        }

        _layout.Dock.Retain(IsShownPanel);
    }

    private void ShowInRestoredPlace(DocumentTab tab)
    {
        if (_documents.HasView(tab))
        {
            return;
        }

        var panel = _documents.CreatePanel();
        _documents.AddView(tab, panel);
        if (DockPanels.TryPlaceDocument(_layout.Dock, panel, _documents.IsTabPanel))
        {
            return;
        }

        Docking.DockDocument(_layout, panel, panel);
    }

    private bool IsShownPanel(string panel)
    {
        if (DockPanels.IsView(panel))
        {
            return true;
        }

        return _documents.IsTabPanel(panel);
    }

    /// <summary>Focuses the tab of the panel the layout focuses now, else the first group's front tab, among
    /// <paramref name="navigation"/>'s, as <see cref="FocusTab"/> does.</summary>
    public void FocusNextTab(Navigation navigation, DecompilerSetup setup)
    {
        var next = _documents.FindTab(_layout.Dock.Focused);
        if (next is null)
        {
            next = FindFrontTab();
        }

        if (next is null)
        {
            return;
        }

        FocusTab(navigation, setup, next);
    }

    // Loops, not LINQ, so it allocates nothing.
    private DocumentTab? FindFrontTab()
    {
        var dock = _layout.Dock;
        var docked = FindFrontTab(dock.Root);
        if (docked is not null)
        {
            return docked;
        }

        foreach (var window in dock.Windows)
        {
            var floating = FindFrontTab(window.Root);
            if (floating is not null)
            {
                return floating;
            }
        }
        return null;
    }

    private DocumentTab? FindFrontTab(DockNode node)
    {
        if (node is DockGroup group)
        {
            return _documents.FindTab(group.Active);
        }

        if (node is not DockSplit split)
        {
            return null;
        }

        var first = FindFrontTab(split.First);
        if (first is not null)
        {
            return first;
        }

        return FindFrontTab(split.Second);
    }

    private void FocusTab(Navigation navigation, DecompilerSetup setup, DocumentTab tab)
    {
        Commands.DecompileLater(_commands, navigation.FocusTab(tab, setup));
    }

    /// <summary>Focuses another tab when the active one was closed by a feature, as <see cref="FocusNextTab"/> does,
    /// trying again only once the tabs or the layout changed since the last try found none.</summary>
    public void FocusNextTabIfNone(Navigation navigation, DecompilerSetup setup)
    {
        if (navigation.ActiveTab is not null)
        {
            return;
        }

        var attempt = new TabFocusTry { LayoutChanges = _layout.Dock.Changes, TabCount = navigation.OpenTabs.Count };
        if (!_tabFocus.TryNewAttempt(attempt))
        {
            return;
        }

        FocusNextTab(navigation, setup);
    }

    /// <summary>Docks <paramref name="tab"/>, an open tab, beside the tab <paramref name="beside"/> when it
    /// is not docked yet, and brings it to the front.</summary>
    public void ShowTab(DocumentTab tab, DocumentTab? beside)
    {
        _documents.AddView(tab);
        var panel = _documents.GetPanel(tab);
        if (!_layout.Dock.Contains(panel))
        {
            Docking.DockDocument(_layout, panel, _documents.FindPanel(beside));
        }

        _layout.Dock.Activate(panel);
    }

    /// <summary>Brings <paramref name="tab"/>'s panel to the front and makes it <paramref name="navigation"/>'s active
    /// tab, as <see cref="FocusTab"/> does.</summary>
    public void ActivateTab(Navigation navigation, DecompilerSetup setup, DocumentTab tab)
    {
        if (!_documents.HasView(tab))
        {
            return;
        }

        _layout.Dock.Activate(_documents.GetPanel(tab));
        FocusTab(navigation, setup, tab);
    }

    /// <summary>Called after the documents are drawn: makes the tab the user last pressed, docked or brought to
    /// front <paramref name="navigation"/>'s active tab, as <see cref="FocusTab"/> does, unless it is.</summary>
    public void ActivateFocusedTab(Navigation navigation, DecompilerSetup setup)
    {
        var active = navigation.ActiveTab;
        _tabFocus.RecordActiveTab(active);
        var tab = _documents.FindTab(_layout.Dock.Focused);
        if (tab is null)
        {
            return;
        }

        if (tab == active)
        {
            return;
        }

        FocusTab(navigation, setup, tab);
    }

    /// <summary>Brings the next tab of the active tab's group to the front, or the previous one, as
    /// <see cref="ActivateTab"/> does.</summary>
    public void CycleTab(Navigation navigation, DecompilerSetup setup, int by)
    {
        var active = _tabFocus.Active;
        if (active is null)
        {
            return;
        }

        var activePanel = _documents.GetPanel(active);
        var group = _layout.Dock.GroupOf(activePanel);
        if (group is null)
        {
            return;
        }

        var panels = group.Panels.FindAll(_documents.IsTabPanel);
        if (panels.Count < 2)
        {
            return;
        }

        var index = panels.IndexOf(activePanel);
        var next = panels[WrapIndex(index + by, panels.Count)];
        var tab = _documents.FindTab(next);
        if (tab is null)
        {
            return;
        }

        ActivateTab(navigation, setup, tab);
    }

    private static int WrapIndex(int index, int count)
    {
        var wrapped = index % count;
        if (wrapped < 0)
        {
            wrapped += count;
        }

        return wrapped;
    }

    public void CloseActiveTab()
    {
        var tab = _tabFocus.Active;
        if (tab is null)
        {
            return;
        }

        CloseTab(tab);
    }

    /// <summary>Closes <paramref name="tab"/>'s panel, which asks for the tab to be closed.</summary>
    public void CloseTab(DocumentTab tab)
    {
        _layout.Dock.Close(_documents.GetPanel(tab));
    }

    /// <summary>Closes the document tabs of <paramref name="tab"/>'s group but <paramref name="tab"/>.</summary>
    public void CloseOtherTabs(DocumentTab tab)
    {
        var others = ListTabsInGroup(tab);
        others.Remove(tab);
        CloseTabs(others);
    }

    /// <summary>Closes every document tab of <paramref name="tab"/>'s group.</summary>
    public void CloseTabsInGroup(DocumentTab tab)
    {
        CloseTabs(ListTabsInGroup(tab));
    }

    /// <summary>Closes the document tabs before <paramref name="tab"/> in its group.</summary>
    public void CloseTabsLeftOf(DocumentTab tab)
    {
        var tabs = ListTabsInGroup(tab);
        var index = tabs.IndexOf(tab);
        CloseTabs(tabs.GetRange(0, index));
    }

    /// <summary>Closes the document tabs after <paramref name="tab"/> in its group.</summary>
    public void CloseTabsRightOf(DocumentTab tab)
    {
        var tabs = ListTabsInGroup(tab);
        var after = tabs.IndexOf(tab) + 1;
        CloseTabs(tabs.GetRange(after, tabs.Count - after));
    }

    /// <summary>Closes the document tabs of every group, docked or floating.</summary>
    public void CloseAllTabs()
    {
        var all = new List<DocumentTab>();
        foreach (var group in _layout.Dock.AllGroups())
        {
            AddTabsOf(all, group);
        }

        CloseTabs(all);
    }

    /// <summary>The document tabs of <paramref name="tab"/>'s group, in the order its strip shows
    /// them, leaving out the other panels docked there.</summary>
    public List<DocumentTab> ListTabsInGroup(DocumentTab tab)
    {
        var tabs = new List<DocumentTab>();
        var group = _layout.Dock.GroupOf(_documents.GetPanel(tab));
        if (group is null)
        {
            return tabs;
        }

        AddTabsOf(tabs, group);
        return tabs;
    }

    private void AddTabsOf(List<DocumentTab> tabs, DockGroup group)
    {
        foreach (var panel in group.Panels)
        {
            AddTabOf(tabs, panel);
        }
    }

    private void AddTabOf(List<DocumentTab> tabs, string? panel)
    {
        var tab = _documents.FindTab(panel);
        if (tab is null)
        {
            return;
        }

        tabs.Add(tab);
    }

    private void CloseTabs(List<DocumentTab> tabs)
    {
        foreach (var tab in tabs)
        {
            CloseTab(tab);
        }
    }

    /// <summary>Drops the view of each tab closed, keeping its image to free, and closes its panel; and
    /// makes a view for each tab opened that has none yet.</summary>
    public void AddAndDropTabViews(Navigation navigation)
    {
        DropClosedTabViews(navigation);
        AddOpenTabViews(navigation);
    }

    private void DropClosedTabViews(Navigation navigation)
    {
        var closed = _documents.ListClosedTabs(navigation);
        for (var i = 0; i < closed.Count; i++)
        {
            var tab = closed[i];
            var panel = _documents.GetPanel(tab);
            _documents.RemoveView(tab);
            ClosePanel(panel);
        }
    }

    private void ClosePanel(string panel)
    {
        if (!_layout.Dock.Contains(panel))
        {
            return;
        }

        _layout.Dock.Close(panel);
    }

    private void AddOpenTabViews(Navigation navigation)
    {
        for (var i = 0; i < navigation.OpenTabs.Count; i++)
        {
            _documents.AddView(navigation.OpenTabs[i]);
        }
    }

    /// <summary>Shows code in <paramref name="language"/> from now on, as the user picked it: <paramref name="decompiler"/>
    /// takes it in this frame, and the tabs in front that showed another start loading again in it, drawn loading from
    /// this frame on, only their decompiling queued. The app loop's redo of the decompilation finds them current
    /// (<see cref="Decompilation.StartDecompilingAgainIfOutdated"/> starts each once).</summary>
    public void ShowInLanguage(DecompilerChoice decompiler, DecompilationLanguage language)
    {
        decompiler.UseLanguage(language);
        var setup = decompiler.Setup;
        foreach (var tab in ListFrontTabs())
        {
            Commands.DecompileLater(_commands, Decompilation.StartDecompilingAgainIfOutdated(tab, setup.Language, setup.Options));
        }
    }

    /// <summary>The tab in front of each group that has one.</summary>
    public List<DocumentTab> ListFrontTabs()
    {
        var tabs = new List<DocumentTab>();
        foreach (var group in _layout.Dock.AllGroups())
        {
            AddTabOf(tabs, group.Active);
        }

        return tabs;
    }
}
