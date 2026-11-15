using ILSight.Core;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core.Metadata;
using ILSight.CodeViewArea;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.MetadataGridArea;
using ILSight.SearchArea;
using ILSight.DiagramArea;
using ILSight.MainArea;

namespace ILSight;

/// <summary>The open tabs as panels of the layout: docking them, the reopened ones where they were docked,
/// bringing them to the front, cycling and closing them, and keeping a view for each open tab. What changes the tabs themselves is asked of the
/// app state through the screen's command writer.</summary>
public static class DocumentPanels
{
    /// <summary>How <paramref name="tab"/>, an open tab, is shown.</summary>
    public static TabView GetView(DocumentViews documents, DocumentTab tab)
    {
        return documents.Views[tab];
    }

    /// <summary>The panel <paramref name="tab"/>, an open tab, is docked as.</summary>
    public static string GetPanel(DocumentViews documents, DocumentTab tab)
    {
        return GetView(documents, tab).Panel;
    }

    public static bool HasView(DocumentViews documents, DocumentTab tab)
    {
        return documents.Views.ContainsKey(tab);
    }

    /// <summary>The panel <paramref name="tab"/> is docked as, or null when there is no tab or it has no view.</summary>
    public static string? FindPanel(DocumentViews documents, DocumentTab? tab)
    {
        if (tab is null)
        {
            return null;
        }

        if (!documents.Views.TryGetValue(tab, out var view))
        {
            return null;
        }

        return view.Panel;
    }

    /// <summary>The panel <paramref name="tab"/> is docked as, and how its diagram or comparison is viewed, as the
    /// settings keep it (<see cref="SavePanels"/>); null when it has no view.</summary>
    public static TabPanel? FindTabPanel(DocumentViews documents, DocumentTab tab)
    {
        if (!documents.Views.TryGetValue(tab, out var view))
        {
            return null;
        }

        return new TabPanel { Tab = tab, Panel = view.Panel, View = SaveView(tab, view) };
    }

    /// <summary>The panel each tab with a view is docked as, and how its diagram or comparison is viewed,
    /// for the settings to keep.</summary>
    public static List<TabPanel> SavePanels(DocumentViews documents)
    {
        var panels = new List<TabPanel>();
        foreach (var (tab, view) in documents.Views)
        {
            panels.Add(new TabPanel { Tab = tab, Panel = view.Panel, View = SaveView(tab, view) });
        }

        return panels;
    }

    /// <summary>The view of <paramref name="tab"/>, or null when it has none.</summary>
    public static TabView? FindView(DocumentViews documents, DocumentTab tab)
    {
        return documents.Views.GetValueOrDefault(tab);
    }

    /// <summary>Returns the view of <paramref name="tab"/>, an open tab, to <paramref name="position"/>, when it is
    /// a position of the view the tab shows, a grid's or a code view's.</summary>
    public static void RestoreView(DocumentViews documents, DocumentTab tab, ViewPosition position)
    {
        var view = documents.Views.GetValueOrDefault(tab);
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

    /// <summary>Returns the view of the diagram <paramref name="tab"/>, an open tab, shows to how
    /// <paramref name="saved"/> says it was.</summary>
    public static void RestoreDiagramView(DocumentViews documents, DocumentTab tab, DiagramViewRecord saved)
    {
        var view = documents.Views.GetValueOrDefault(tab);
        if (view is null)
        {
            return;
        }

        view.Diagram.RestoreView(saved);
    }

    /// <summary>Returns the view of the comparison <paramref name="tab"/>, an open tab, shows to how
    /// <paramref name="saved"/> says it was.</summary>
    public static void RestoreComparisonView(DocumentViews documents, DocumentTab tab, ComparisonViewRecord saved)
    {
        var view = documents.Views.GetValueOrDefault(tab);
        if (view is null)
        {
            return;
        }

        view.Comparison.RestoreView(saved);
    }

    /// <summary>Scrolls the code of <paramref name="tab"/>, an open tab, to <paramref name="line"/> and flashes it.</summary>
    public static void RevealLine(DocumentViews documents, DocumentTab tab, int line)
    {
        var view = documents.Views.GetValueOrDefault(tab);
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
    public static void RevealRow(DocumentViews documents, DocumentTab tab, int row)
    {
        var view = documents.Views.GetValueOrDefault(tab);
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
    public static void FocusCard(DocumentViews documents, DocumentTab tab, CodeReference.Entity type)
    {
        var view = documents.Views.GetValueOrDefault(tab);
        if (view is null)
        {
            return;
        }

        view.Diagram.FocusCard(type);
    }

    /// <summary>What <paramref name="view"/> counts toward <see cref="DocumentViews.Revision"/>.</summary>
    public static int CountViewChanges(TabView view)
    {
        return view.Diagram.Changes + view.Comparison.Changes;
    }

    private static TabPanelView SaveView(DocumentTab tab, TabView view)
    {
        if (tab.Content is TabContent.Diagram)
        {
            return new TabPanelView.Diagram(Diagrams.SaveView(view.Diagram));
        }

        if (tab.Content is TabContent.Comparison)
        {
            return new TabPanelView.Comparison(view.Comparison.SaveView());
        }

        return new TabPanelView.None();
    }

    /// <summary>Shows the reopened <paramref name="tabs"/> in the panels they were docked as: in place where
    /// the layout still has one no tab is shown in, else, renamed when another tab is shown in one by that
    /// name, arranged as <paramref name="saved"/>, the documents' saved arrangement, has them, beside the
    /// documents; returns their diagrams' views to how they were saved, and brings the active tab to the front.</summary>
    public static void ShowReopenedTabs(MainLayout layout, DocumentViews documents, TabFocus tabFocus, IReadOnlyList<ReopenedTab> tabs, DockLayout? saved)
    {
        var panels = new Dictionary<string, string>();
        foreach (var reopened in tabs)
        {
            panels[reopened.Record.Panel] = AddReopenedView(documents, reopened);
        }

        DockUndocked(layout, panels, saved);
        ActivateActivePanel(documents, tabFocus, layout);
    }

    private static string AddReopenedView(DocumentViews documents, ReopenedTab reopened)
    {
        var panel = ChooseReopenedPanel(documents, reopened.Record.Panel);
        documents.AddView(reopened.Tab, panel);
        RestoreDiagramView(documents, reopened);
        RestoreComparisonView(documents, reopened);
        return panel;
    }

    private static string ChooseReopenedPanel(DocumentViews documents, string saved)
    {
        if (!IsTabPanel(documents, saved))
        {
            return saved;
        }

        return documents.CreatePanel();
    }

    private static void RestoreDiagramView(DocumentViews documents, ReopenedTab reopened)
    {
        if (reopened.Record is not DiagramDocumentRecord diagram)
        {
            return;
        }

        var view = diagram.View;
        if (view is null)
        {
            return;
        }

        DocumentPanels.RestoreDiagramView(documents, reopened.Tab, view);
    }

    private static void RestoreComparisonView(DocumentViews documents, ReopenedTab reopened)
    {
        if (reopened.Record is not ComparisonDocumentRecord comparison)
        {
            return;
        }

        var view = comparison.View;
        if (view is null)
        {
            return;
        }

        DocumentPanels.RestoreComparisonView(documents, reopened.Tab, view);
    }

    private static void DockUndocked(MainLayout layout, Dictionary<string, string> panels, DockLayout? saved)
    {
        var undocked = panels.Values
            .Where(p => !layout.Dock.Contains(p))
            .ToList();
        if (undocked.Count == 0)
        {
            return;
        }

        var arrangement = ArrangeUndocked(undocked, panels, saved);
        Docking.DockArranged(layout, undocked, arrangement);
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

    /// <summary>Brings the active tab's panel to the front and focuses it, when the layout has it.</summary>
    internal static void ActivateActivePanel(DocumentViews documents, TabFocus tabFocus, MainLayout layout)
    {
        var panel = FindActivePanel(documents, tabFocus);
        if (panel is null)
        {
            return;
        }

        if (!layout.Dock.Contains(panel))
        {
            return;
        }

        layout.Dock.Activate(panel);
    }

    /// <summary>Shows each open tab no panel was given, such as the one a run starts with when the open
    /// list kept no documents, in the place of the documents the layout was restored with, then takes the
    /// panels of the documents no tab is shown in out of the layout.</summary>
    public static void ShowInRestoredDocuments(MainLayout layout, DocumentViews documents, Navigation navigation)
    {
        for (var i = 0; i < navigation.OpenTabs.Count; i++)
        {
            ShowInRestoredPlace(layout, documents, navigation.OpenTabs[i]);
        }

        layout.Dock.Retain(p => IsShownPanel(documents, p));
    }

    private static void ShowInRestoredPlace(MainLayout layout, DocumentViews documents, DocumentTab tab)
    {
        if (DocumentPanels.HasView(documents, tab))
        {
            return;
        }

        var panel = documents.CreatePanel();
        documents.AddView(tab, panel);
        if (DockPanels.TryPlaceDocument(layout.Dock, panel, p => IsTabPanel(documents, p)))
        {
            return;
        }

        Docking.DockDocument(layout, panel, panel);
    }

    private static bool IsShownPanel(DocumentViews documents, string panel)
    {
        if (DockPanels.IsView(panel))
        {
            return true;
        }

        return IsTabPanel(documents, panel);
    }

    /// <summary>A new panel the layout makes for itself, with an empty tab asked for to be opened in it.</summary>
    internal static string OpenTabForPanel(MainLayout layout, DocumentViews documents, IAppCommandWriter commands)
    {
        var panel = documents.CreatePanel();
        Commands.Later(commands, (layout, documents, panel), static (a, s) => QueuedCommands.OpenTabIn(s.layout, s.documents, a, s.panel, focus: false));
        return panel;
    }

    /// <summary>Called when a panel is closed: clears the search with its panel, and asks for the tab a
    /// closed document was to be closed.</summary>
    internal static void OnPanelClosed(MainLayout layout, DocumentViews documents, Search search, IAppCommandWriter commands, string panel)
    {
        if (panel == DockPanels.Search)
        {
            search.Clear();
        }

        CloseTabOf(layout, documents, commands, panel);
    }

    private static void CloseTabOf(MainLayout layout, DocumentViews documents, IAppCommandWriter commands, string panel)
    {
        var tab = documents.FindTab(panel);
        if (tab is null)
        {
            return;
        }

        documents.RemoveView(tab);
        Commands.Later(commands, (layout, documents, commands, tab), static (a, s) => QueuedCommands.CloseTabAndFocusNext(s.layout, s.documents, s.commands, a, s.tab));
    }

    /// <summary>Focuses the tab of the panel the layout focuses now, else the first group's front tab, among
    /// <paramref name="navigation"/>'s, as <see cref="FocusTab"/> does.</summary>
    public static void FocusNextTab(MainLayout layout, DocumentViews documents, IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup)
    {
        var next = documents.FindTab(layout.Dock.Focused);
        if (next is null)
        {
            next = FindFrontTab(layout, documents);
        }

        if (next is null)
        {
            return;
        }

        FocusTab(commands, navigation, setup, next);
    }

    // Loops, not LINQ, so it allocates nothing.
    private static DocumentTab? FindFrontTab(MainLayout layout, DocumentViews documents)
    {
        var dock = layout.Dock;
        var docked = FindFrontTab(documents, dock.Root);
        if (docked is not null)
        {
            return docked;
        }

        foreach (var window in dock.Windows)
        {
            var floating = FindFrontTab(documents, window.Root);
            if (floating is not null)
            {
                return floating;
            }
        }
        return null;
    }

    private static DocumentTab? FindFrontTab(DocumentViews documents, DockNode node)
    {
        if (node is DockGroup group)
        {
            return documents.FindTab(group.Active);
        }

        if (node is not DockSplit split)
        {
            return null;
        }

        var first = FindFrontTab(documents, split.First);
        if (first is not null)
        {
            return first;
        }

        return FindFrontTab(documents, split.Second);
    }

    private static void FocusTab(IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup, DocumentTab tab)
    {
        Commands.DecompileLater(commands, navigation.FocusTab(tab, setup));
    }

    /// <summary>Focuses another tab when the active one was closed by a feature, as <see cref="FocusNextTab"/> does,
    /// trying again only once the tabs or the layout changed since the last try found none.</summary>
    public static void FocusNextTabIfNone(MainLayout layout, DocumentViews documents, TabFocus tabFocus, IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup)
    {
        if (navigation.ActiveTab is not null)
        {
            return;
        }

        var attempt = new TabFocusTry { LayoutChanges = layout.Dock.Changes, TabCount = navigation.OpenTabs.Count };
        if (!tabFocus.TryNewAttempt(attempt))
        {
            return;
        }

        FocusNextTab(layout, documents, commands, navigation, setup);
    }

    /// <summary>Docks <paramref name="tab"/>, an open tab, beside the tab <paramref name="beside"/> when it
    /// is not docked yet, and brings it to the front.</summary>
    public static void ShowTab(MainLayout layout, DocumentViews documents, DocumentTab tab, DocumentTab? beside)
    {
        documents.AddView(tab);
        var panel = DocumentPanels.GetPanel(documents, tab);
        if (!layout.Dock.Contains(panel))
        {
            Docking.DockDocument(layout, panel, DocumentPanels.FindPanel(documents, beside));
        }

        layout.Dock.Activate(panel);
    }

    /// <summary>Brings <paramref name="tab"/>'s panel to the front and makes it <paramref name="navigation"/>'s active
    /// tab, as <see cref="FocusTab"/> does.</summary>
    public static void ActivateTab(MainLayout layout, DocumentViews documents, IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup, DocumentTab tab)
    {
        if (!DocumentPanels.HasView(documents, tab))
        {
            return;
        }

        layout.Dock.Activate(DocumentPanels.GetPanel(documents, tab));
        FocusTab(commands, navigation, setup, tab);
    }

    /// <summary>Called after the documents are drawn: makes the tab the user last pressed, docked or brought to
    /// front <paramref name="navigation"/>'s active tab, as <see cref="FocusTab"/> does, unless it is.</summary>
    public static void SyncActiveTab(MainLayout layout, DocumentViews documents, TabFocus tabFocus, IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup)
    {
        var active = navigation.ActiveTab;
        tabFocus.Follow(active);
        var tab = documents.FindTab(layout.Dock.Focused);
        if (tab is null)
        {
            return;
        }

        if (tab == active)
        {
            return;
        }

        FocusTab(commands, navigation, setup, tab);
    }

    /// <summary>Brings the next tab of the active tab's group to the front, or the previous one, as
    /// <see cref="ActivateTab"/> does.</summary>
    public static void CycleTab(MainLayout layout, DocumentViews documents, TabFocus tabFocus, IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup, int by)
    {
        var active = tabFocus.Active;
        if (active is null)
        {
            return;
        }

        var activePanel = DocumentPanels.GetPanel(documents, active);
        var group = layout.Dock.GroupOf(activePanel);
        if (group is null)
        {
            return;
        }

        var panels = group.Panels.FindAll(p => IsTabPanel(documents, p));
        if (panels.Count < 2)
        {
            return;
        }

        var index = panels.IndexOf(activePanel);
        var next = panels[WrapIndex(index + by, panels.Count)];
        var tab = documents.FindTab(next);
        if (tab is null)
        {
            return;
        }

        ActivateTab(layout, documents, commands, navigation, setup, tab);
    }

    private static bool IsTabPanel(DocumentViews documents, string panel)
    {
        return documents.FindTab(panel) is not null;
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

    public static void CloseActiveTab(MainLayout layout, DocumentViews documents, TabFocus tabFocus)
    {
        var tab = tabFocus.Active;
        if (tab is null)
        {
            return;
        }

        CloseTab(layout, documents, tab);
    }

    /// <summary>Closes <paramref name="tab"/>'s panel, which asks for the tab to be closed.</summary>
    public static void CloseTab(MainLayout layout, DocumentViews documents, DocumentTab tab)
    {
        layout.Dock.Close(DocumentPanels.GetPanel(documents, tab));
    }

    /// <summary>Closes the document tabs of <paramref name="tab"/>'s group but <paramref name="tab"/>.</summary>
    public static void CloseOtherTabs(MainLayout layout, DocumentViews documents, DocumentTab tab)
    {
        var others = ListTabsInGroup(layout, documents, tab);
        others.Remove(tab);
        CloseTabs(layout, documents, others);
    }

    /// <summary>Closes every document tab of <paramref name="tab"/>'s group.</summary>
    public static void CloseTabsInGroup(MainLayout layout, DocumentViews documents, DocumentTab tab)
    {
        var all = ListTabsInGroup(layout, documents, tab);
        CloseTabs(layout, documents, all);
    }

    /// <summary>Closes the document tabs before <paramref name="tab"/> in its group.</summary>
    public static void CloseTabsLeftOf(MainLayout layout, DocumentViews documents, DocumentTab tab)
    {
        var tabs = ListTabsInGroup(layout, documents, tab);
        var index = tabs.IndexOf(tab);
        var left = tabs.GetRange(0, index);
        CloseTabs(layout, documents, left);
    }

    /// <summary>Closes the document tabs after <paramref name="tab"/> in its group.</summary>
    public static void CloseTabsRightOf(MainLayout layout, DocumentViews documents, DocumentTab tab)
    {
        var tabs = ListTabsInGroup(layout, documents, tab);
        var after = tabs.IndexOf(tab) + 1;
        var right = tabs.GetRange(after, tabs.Count - after);
        CloseTabs(layout, documents, right);
    }

    /// <summary>Closes the document tabs of every group, docked or floating.</summary>
    public static void CloseAllTabs(MainLayout layout, DocumentViews documents)
    {
        var all = new List<DocumentTab>();
        foreach (var group in layout.Dock.AllGroups())
        {
            AddTabsOf(documents, all, group);
        }

        CloseTabs(layout, documents, all);
    }

    /// <summary>The document tabs of <paramref name="tab"/>'s group, in the order its strip shows
    /// them, leaving out the other panels docked there.</summary>
    public static List<DocumentTab> ListTabsInGroup(MainLayout layout, DocumentViews documents, DocumentTab tab)
    {
        var tabs = new List<DocumentTab>();
        var group = layout.Dock.GroupOf(DocumentPanels.GetPanel(documents, tab));
        if (group is null)
        {
            return tabs;
        }

        AddTabsOf(documents, tabs, group);
        return tabs;
    }

    private static void AddTabsOf(DocumentViews documents, List<DocumentTab> tabs, DockGroup group)
    {
        foreach (var panel in group.Panels)
        {
            AddTabOf(documents, tabs, panel);
        }
    }

    private static void AddTabOf(DocumentViews documents, List<DocumentTab> tabs, string? panel)
    {
        var tab = documents.FindTab(panel);
        if (tab is null)
        {
            return;
        }

        tabs.Add(tab);
    }

    private static void CloseTabs(MainLayout layout, DocumentViews documents, List<DocumentTab> tabs)
    {
        foreach (var tab in tabs)
        {
            CloseTab(layout, documents, tab);
        }
    }

    /// <summary>The panel of the active tab, which the views dock beside, or null.</summary>
    internal static string? FindActivePanel(DocumentViews documents, TabFocus tabFocus)
    {
        return DocumentPanels.FindPanel(documents, tabFocus.Active);
    }

    /// <summary>Drops the view of each tab closed, keeping its image to free, and closes its panel; and
    /// makes a view for each tab opened that has none yet.</summary>
    public static void SyncTabViews(MainLayout layout, DocumentViews documents, Navigation navigation)
    {
        DropClosedTabViews(layout, documents, navigation);
        AddOpenTabViews(documents, navigation);
    }

    private static void DropClosedTabViews(MainLayout layout, DocumentViews documents, Navigation navigation)
    {
        var closed = documents.ListClosedTabs(navigation);
        for (var i = 0; i < closed.Count; i++)
        {
            var tab = closed[i];
            var panel = DocumentPanels.GetPanel(documents, tab);
            documents.RemoveView(tab);
            ClosePanel(layout.Dock, panel);
        }
    }

    private static void ClosePanel(DockLayout layout, string panel)
    {
        if (!layout.Contains(panel))
        {
            return;
        }

        layout.Close(panel);
    }

    private static void AddOpenTabViews(DocumentViews documents, Navigation navigation)
    {
        for (var i = 0; i < navigation.OpenTabs.Count; i++)
        {
            documents.AddView(navigation.OpenTabs[i]);
        }
    }

    /// <summary>Shows code in <paramref name="language"/> from now on, as the user picked it: <paramref name="decompiler"/>
    /// takes it in this frame, and the tabs in front that showed another start loading again in it, drawn loading from
    /// this frame on, only their decompiling queued. The frame edge's follow finds them current
    /// (<see cref="Decompilation.StartDecompilingAgainIfOutdated"/> starts each once).</summary>
    public static void ShowInLanguage(MainLayout layout, DocumentViews documents, IAppCommandWriter commands, DecompilerChoice decompiler, DecompilationLanguage language)
    {
        decompiler.UseLanguage(language);
        var setup = decompiler.Setup;
        foreach (var tab in ListFrontTabs(layout, documents))
        {
            Commands.DecompileLater(commands, Decompilation.StartDecompilingAgainIfOutdated(tab, setup.Language, setup.Options));
        }
    }

    /// <summary>The tab in front of each group that has one.</summary>
    public static List<DocumentTab> ListFrontTabs(MainLayout layout, DocumentViews documents)
    {
        var tabs = new List<DocumentTab>();
        foreach (var group in layout.Dock.AllGroups())
        {
            AddTabOf(documents, tabs, group.Active);
        }

        return tabs;
    }
}
