using System.Diagnostics;
using ILSight.Core;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core.Metadata;

namespace ILSight;

/// <summary>The open tabs as panels of the layout: docking them, the reopened ones where they were docked,
/// bringing them to the front, cycling and closing them, and keeping a view for each open tab. What changes the tabs themselves is asked of the
/// app state through <see cref="UiState.AppCommands"/>.</summary>
public static class DocumentPanels
{
    private const string PanelPrefix = "document-";

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
        if (tab == null)
            return null;
        if (!documents.Views.TryGetValue(tab, out var view))
            return null;
        return view.Panel;
    }

    /// <summary>The open tab docked as <paramref name="panel"/>. Allocates nothing.</summary>
    public static DocumentTab? FindTab(DocumentViews documents, string? panel)
    {
        foreach (var (tab, view) in documents.Views)
        {
            if (view.Panel == panel)
                return tab;
        }
        return null;
    }

    /// <summary>A document panel no tab was shown in before, nor any of those <see cref="SkipPanels"/> was given.</summary>
    public static string CreatePanel(DocumentViews documents)
    {
        var panel = NextPanel(documents);
        while (documents.Skipped.Contains(panel))
            panel = NextPanel(documents);
        return panel;
    }

    private static string NextPanel(DocumentViews documents)
    {
        documents.Panels++;
        return $"{PanelPrefix}{documents.Panels}";
    }

    /// <summary>Leaves <paramref name="panels"/>, which saved documents were docked as, to them:
    /// <see cref="NewPanel"/> hands out none of them.</summary>
    public static void SkipPanels(DocumentViews documents, IEnumerable<string> panels)
    {
        foreach (var panel in panels)
            documents.Skipped.Add(panel);
    }

    /// <summary>The panel each tab with a view is docked as, and how its diagram or comparison is viewed,
    /// for the settings to keep.</summary>
    public static List<TabPanel> SavePanels(DocumentViews documents)
    {
        var panels = new List<TabPanel>();
        foreach (var (tab, view) in documents.Views)
            panels.Add(new TabPanel(tab, view.Panel, SaveView(tab, view)));
        return panels;
    }

    /// <summary>Shows <paramref name="tab"/> in a new panel, unless it is shown already.</summary>
    public static void AddView(DocumentViews documents, DocumentTab tab)
    {
        if (HasView(documents, tab))
            return;
        AddView(documents, tab, CreatePanel(documents));
    }

    /// <summary>Shows <paramref name="tab"/> in <paramref name="panel"/>, unless it is shown already, its view
    /// following what the tab shows from the start, so what is asked of the view before it is next drawn,
    /// such as a row to reveal, is not undone when it first takes the tab's content.</summary>
    public static void AddView(DocumentViews documents, DocumentTab tab, string panel)
    {
        if (HasView(documents, tab))
            return;
        var view = new TabView { Panel = panel, ImageId = $"{panel}-image" };
        PictureViews.Follow(view.Picture, tab.Image);
        documents.Views.Add(tab, view);
        documents.OwnChanges++;
    }

    /// <summary>Drops the view of <paramref name="tab"/>, keeping its image to free.</summary>
    public static void RemoveView(DocumentViews documents, DocumentTab tab)
    {
        if (!documents.Views.Remove(tab, out var view))
            return;
        documents.ReleasedImages.Add(view.ImageId);
        documents.DroppedChanges += CountViewChanges(view);
        documents.OwnChanges++;
    }

    /// <summary>The tabs with a view that <paramref name="tabs"/> no longer holds, in a list the next call reuses.</summary>
    public static IReadOnlyList<DocumentTab> ListClosedTabs(DocumentViews documents, DocumentTabs tabs)
    {
        documents.Closed.Clear();
        foreach (var tab in documents.Views.Keys)
        {
            if (!Tabs.IsOpen(tabs, tab))
                documents.Closed.Add(tab);
        }
        return documents.Closed;
    }

    /// <summary>Lets each view follow the grid or picture its tab shows.</summary>
    public static void FollowViewContent(DocumentViews documents)
    {
        foreach (var (tab, view) in documents.Views)
            PictureViews.Follow(view.Picture, tab.Image);
    }

    /// <summary>Where the view of <paramref name="tab"/> stands, or null when it has none.</summary>
    public static ViewPosition? FindViewPosition(DocumentViews documents, DocumentTab tab)
    {
        if (!documents.Views.TryGetValue(tab, out var view))
            return null;
        return GetPosition(view, tab);
    }

    /// <summary>The images of the views dropped since the last call, for the canvas to free.</summary>
    public static IReadOnlyList<string> TakeReleasedImages(DocumentViews documents)
    {
        if (documents.ReleasedImages.Count == 0)
            return Array.Empty<string>();
        var released = documents.ReleasedImages.ToArray();
        documents.ReleasedImages.Clear();
        return released;
    }

    /// <summary>Returns the view of <paramref name="tab"/>, an open tab, to <paramref name="position"/>, when it is
    /// a position of the view the tab shows, a grid's or a code view's.</summary>
    public static void RestoreView(DocumentViews documents, DocumentTab tab, ViewPosition position)
    {
        var view = documents.Views.GetValueOrDefault(tab);
        if (view == null)
            return;
        var shown = tab.Grid;
        if (shown == null)
        {
            var code = position.Code;
            if (code != null)
                CodeViewWidget.RestorePosition(view.Code, code.Value);
            return;
        }
        var grid = position.Grid;
        if (grid != null)
            MetadataGridWidget.RestorePosition(GetGridState(view, shown), grid.Value);
    }

    /// <summary>Returns the view of the diagram <paramref name="tab"/>, an open tab, shows to how
    /// <paramref name="saved"/> says it was.</summary>
    public static void RestoreDiagramView(DocumentViews documents, DocumentTab tab, DiagramViewRecord saved)
    {
        var view = documents.Views.GetValueOrDefault(tab);
        if (view == null)
            return;
        Diagrams.RestoreView(view.Diagram, saved);
    }

    /// <summary>Returns the view of the comparison <paramref name="tab"/>, an open tab, shows to how
    /// <paramref name="saved"/> says it was.</summary>
    public static void RestoreComparisonView(DocumentViews documents, DocumentTab tab, ComparisonViewRecord saved)
    {
        var view = documents.Views.GetValueOrDefault(tab);
        if (view == null)
            return;
        view.Comparison.Restore(saved);
    }

    /// <summary>Scrolls the code of <paramref name="tab"/>, an open tab, to <paramref name="line"/> and flashes it.</summary>
    public static void RevealLine(DocumentViews documents, DocumentTab tab, int line)
    {
        var view = documents.Views.GetValueOrDefault(tab);
        if (view == null)
            return;
        if (line >= tab.Document.Lines.Length)
            return;
        CodeViewWidget.RevealLine(view.Code, line);
    }

    /// <summary>Selects the grid row <paramref name="row"/> of <paramref name="tab"/>, an open tab, and scrolls to it.</summary>
    public static void RevealRow(DocumentViews documents, DocumentTab tab, int row)
    {
        var view = documents.Views.GetValueOrDefault(tab);
        if (view == null)
            return;
        var grid = tab.Grid;
        if (grid == null || row >= grid.RowCount)
            return;
        MetadataGridWidget.RevealRow(GetGridState(view, grid), row);
    }

    /// <summary>Selects the card of <paramref name="type"/> on the diagram <paramref name="tab"/>, an open tab,
    /// shows, and brings it into view once it is laid out.</summary>
    public static void FocusCard(DocumentViews documents, DocumentTab tab, CodeReference.Entity type)
    {
        var view = documents.Views.GetValueOrDefault(tab);
        if (view == null)
            return;
        Diagrams.FocusCard(view.Diagram, type);
    }

    /// <summary>What <paramref name="view"/> counts toward <see cref="DocumentViews.Revision"/>.</summary>
    public static int CountViewChanges(TabView view)
    {
        return view.Diagram.Changes + view.Comparison.Changes;
    }

    private static TabPanelView SaveView(DocumentTab tab, TabView view)
    {
        return tab.Content switch
        {
            TabContent.Diagram => new TabPanelView.Diagram(Diagrams.SaveView(view.Diagram)),
            TabContent.Comparison => new TabPanelView.Comparison(view.Comparison.Save()),
            TabContent.Empty or TabContent.Loading or TabContent.Code or TabContent.Table or TabContent.Picture or TabContent.Report
                or TabContent.Failed or TabContent.Reloading or TabContent.Reopening or TabContent.Gone => new TabPanelView.None(),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The view of <paramref name="grid"/>: the one kept for it, or a new one when the tab
    /// showed another grid, or none, before.</summary>
    public static MetadataGridState GetGridState(TabView view, MetadataGrid grid)
    {
        var shown = view.Grid;
        if (shown != null && ReferenceEquals(shown.Grid, grid))
            return shown;
        shown = MetadataGridWidget.CreateState(grid);
        view.Grid = shown;
        return shown;
    }

    /// <summary>Where the view of what <paramref name="tab"/> shows stands: its grid's or its code's.</summary>
    public static ViewPosition GetPosition(TabView view, DocumentTab tab)
    {
        var grid = tab.Grid;
        if (grid != null)
            return ViewPosition.OfGrid(GetGridState(view, grid).Position);
        return ViewPosition.OfCode(view.Code.Position);
    }

    /// <summary>Shows the reopened <paramref name="tabs"/> in the panels they were docked as: in place where
    /// the layout still has one no tab is shown in, else, renamed when another tab is shown in one by that
    /// name, arranged as <paramref name="layout"/>, the documents' saved arrangement, has them, beside the
    /// documents; returns their diagrams' views to how they were saved, and brings the active tab to the front.</summary>
    public static void ShowReopenedTabs(UiState uiState, IReadOnlyList<ReopenedTab> tabs, DockLayout? layout)
    {
        var panels = new Dictionary<string, string>();
        foreach (var reopened in tabs)
            panels[reopened.Record.Panel] = AddReopenedView(uiState, reopened);
        DockUndocked(uiState, panels, layout);
        ActivateActivePanel(uiState);
    }

    /// <summary>Makes the view of the reopened tab, as it was saved, and returns the panel it is shown in.</summary>
    private static string AddReopenedView(UiState uiState, ReopenedTab reopened)
    {
        var panel = ChooseReopenedPanel(uiState, reopened.Record.Panel);
        DocumentPanels.AddView(uiState.Documents, reopened.Tab, panel);
        RestoreDiagramView(uiState, reopened);
        RestoreComparisonView(uiState, reopened);
        return panel;
    }

    /// <summary>The panel <paramref name="saved"/>, unless a tab is shown in it; else a new one.</summary>
    private static string ChooseReopenedPanel(UiState uiState, string saved)
    {
        if (!IsTabPanel(uiState, saved))
            return saved;
        return DocumentPanels.CreatePanel(uiState.Documents);
    }

    private static void RestoreDiagramView(UiState uiState, ReopenedTab reopened)
    {
        if (reopened.Record is not DiagramDocumentRecord diagram)
            return;
        var view = diagram.View;
        if (view == null)
            return;
        DocumentPanels.RestoreDiagramView(uiState.Documents, reopened.Tab, view);
    }

    private static void RestoreComparisonView(UiState uiState, ReopenedTab reopened)
    {
        if (reopened.Record is not ComparisonDocumentRecord comparison)
            return;
        var view = comparison.View;
        if (view == null)
            return;
        DocumentPanels.RestoreComparisonView(uiState.Documents, reopened.Tab, view);
    }

    /// <summary>Docks the panels of <paramref name="panels"/>, by the names they were saved as, that the
    /// layout does not have, as <paramref name="layout"/> arranges them.</summary>
    private static void DockUndocked(UiState uiState, Dictionary<string, string> panels, DockLayout? layout)
    {
        var undocked = panels.Values
            .Where(p => !uiState.Layout.Contains(p))
            .ToList();
        if (undocked.Count == 0)
            return;
        var arrangement = ArrangeUndocked(undocked, panels, layout);
        Docking.DockArranged(uiState.MainLayout, undocked, arrangement);
    }

    /// <summary>A copy of <paramref name="layout"/> renamed as <paramref name="panels"/> says and holding
    /// <paramref name="undocked"/> alone; one group of them when there is no layout.</summary>
    private static DockLayout ArrangeUndocked(List<string> undocked, Dictionary<string, string> panels, DockLayout? layout)
    {
        if (layout == null)
            return ArrangeInOneGroup(undocked);
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
    internal static void ActivateActivePanel(UiState uiState)
    {
        var panel = FindActivePanel(uiState);
        if (panel == null)
            return;
        if (!uiState.Layout.Contains(panel))
            return;
        uiState.Layout.Activate(panel);
    }

    /// <summary>Shows each open tab no panel was given, such as the one a run starts with when the open
    /// list kept no documents, in the place of the documents the layout was restored with, then takes the
    /// panels of the documents no tab is shown in out of the layout.</summary>
    public static void ShowInRestoredDocuments(UiState uiState, DocumentTabs tabs)
    {
        for (var i = 0; i < tabs.Open.Count; i++)
            ShowInRestoredPlace(uiState, tabs.Open[i]);
        uiState.Layout.Retain(p => IsShownPanel(uiState, p));
    }

    private static void ShowInRestoredPlace(UiState uiState, DocumentTab tab)
    {
        if (DocumentPanels.HasView(uiState.Documents, tab))
            return;
        var panel = DocumentPanels.CreatePanel(uiState.Documents);
        DocumentPanels.AddView(uiState.Documents, tab, panel);
        if (DockPanels.PlaceDocument(uiState.Layout, panel, p => IsTabPanel(uiState, p)))
            return;
        Docking.DockDocument(uiState.MainLayout, panel, panel);
    }

    /// <summary>Whether <paramref name="panel"/> is a view, or a tab is shown in it.</summary>
    private static bool IsShownPanel(UiState uiState, string panel)
    {
        if (DockPanels.IsView(panel))
            return true;
        return IsTabPanel(uiState, panel);
    }

    /// <summary>A new panel the layout makes for itself, with an empty tab asked for to be opened in it.</summary>
    internal static string OpenTabForPanel(UiState uiState)
    {
        var panel = DocumentPanels.CreatePanel(uiState.Documents);
        uiState.Later((uiState, panel), static (a, s) => ILSightApp.OpenTabIn(s.uiState, a, s.panel, focus: false));
        return panel;
    }

    /// <summary>Called when a panel is closed: clears the search with its panel, and asks for the tab a
    /// closed document was to be closed.</summary>
    internal static void OnPanelClosed(UiState uiState, string panel)
    {
        if (panel == DockPanels.Search)
            Searches.Clear(uiState.Search);
        CloseTabOf(uiState, panel);
    }

    private static void CloseTabOf(UiState uiState, string panel)
    {
        var tab = DocumentPanels.FindTab(uiState.Documents, panel);
        if (tab == null)
            return;
        RemoveView(uiState.Documents, tab);
        uiState.Later((uiState, tab), static (a, s) => ILSightApp.CloseTabAndFocusNext(s.uiState, a, s.tab));
    }

    /// <summary>Focuses the tab of the panel the layout focuses now, else the first group's front tab.</summary>
    public static void FocusNextTab(UiState uiState)
    {
        var next = DocumentPanels.FindTab(uiState.Documents, uiState.Layout.Focused);
        if (next == null)
            next = FindFrontTab(uiState);
        if (next == null)
            return;
        AskToFocusTab(uiState, next);
    }

    /// <summary>The tab in front of the first group, docked then floating, that has one in front.
    /// Allocates nothing.</summary>
    private static DocumentTab? FindFrontTab(UiState uiState)
    {
        var layout = uiState.Layout;
        var docked = FindFrontTab(uiState, layout.Root);
        if (docked != null)
            return docked;
        foreach (var window in layout.Windows)
        {
            var floating = FindFrontTab(uiState, window.Root);
            if (floating != null)
                return floating;
        }
        return null;
    }

    private static DocumentTab? FindFrontTab(UiState uiState, DockNode node)
    {
        if (node is DockGroup group)
            return DocumentPanels.FindTab(uiState.Documents, group.Active);
        if (node is not DockSplit split)
            return null;
        var first = FindFrontTab(uiState, split.First);
        if (first != null)
            return first;
        return FindFrontTab(uiState, split.Second);
    }

    /// <summary>Asks for <paramref name="tab"/> to become the active tab.</summary>
    private static void AskToFocusTab(UiState uiState, DocumentTab tab)
    {
        uiState.Later(tab, static (a, t) => Docking.FocusTab(a, t));
    }

    /// <summary>Focuses another tab when the active one was closed by a feature, trying again only once
    /// the tabs or the layout changed since the last try found none.</summary>
    public static void FocusNextTabIfNone(UiState uiState, DocumentTabs tabs)
    {
        if (tabs.Active != null)
            return;
        var attempt = new TabFocusTry(uiState.Layout.Changes, tabs.Open.Count);
        if (attempt == uiState.LastTabFocusTry)
            return;
        uiState.LastTabFocusTry = attempt;
        FocusNextTab(uiState);
    }

    /// <summary>Docks <paramref name="tab"/>, an open tab, beside the tab <paramref name="beside"/> when it
    /// is not docked yet, and brings it to the front.</summary>
    public static void ShowTab(UiState uiState, DocumentTab tab, DocumentTab? beside)
    {
        var documents = uiState.Documents;
        DocumentPanels.AddView(documents, tab);
        var panel = DocumentPanels.GetPanel(documents, tab);
        if (!uiState.Layout.Contains(panel))
            Docking.DockDocument(uiState.MainLayout, panel, DocumentPanels.FindPanel(documents, beside));
        uiState.Layout.Activate(panel);
    }

    /// <summary>Brings <paramref name="tab"/>'s panel to the front and asks for it to become the active tab.</summary>
    public static void ActivateTab(UiState uiState, DocumentTab tab)
    {
        if (!DocumentPanels.HasView(uiState.Documents, tab))
            return;
        uiState.Layout.Activate(DocumentPanels.GetPanel(uiState.Documents, tab));
        AskToFocusTab(uiState, tab);
    }

    /// <summary>Called after the documents are drawn: asks for the tab the user last pressed, docked or
    /// brought to front to become the active one, unless it is <paramref name="active"/>.</summary>
    public static void SyncActiveTab(UiState uiState, DocumentTab? active)
    {
        FollowActiveTab(uiState, active);
        var tab = DocumentPanels.FindTab(uiState.Documents, uiState.Layout.Focused);
        if (tab == null)
            return;
        if (tab == active)
            return;
        AskToFocusTab(uiState, tab);
    }

    /// <summary>Takes <paramref name="active"/>, the app state's active tab, as the tab the views open beside.</summary>
    public static void FollowActiveTab(UiState uiState, DocumentTab? active)
    {
        uiState.ActiveTab = active;
    }

    /// <summary>Brings the next tab of the active tab's group to the front, or the previous one.</summary>
    public static void CycleTab(UiState uiState, int by)
    {
        var active = uiState.ActiveTab;
        if (active == null)
            return;
        var activePanel = DocumentPanels.GetPanel(uiState.Documents, active);
        var group = uiState.Layout.GroupOf(activePanel);
        if (group == null)
            return;
        var panels = group.Panels.FindAll(p => IsTabPanel(uiState, p));
        if (panels.Count < 2)
            return;
        var index = panels.IndexOf(activePanel);
        var next = panels[WrapIndex(index + by, panels.Count)];
        var tab = DocumentPanels.FindTab(uiState.Documents, next);
        if (tab == null)
            return;
        ActivateTab(uiState, tab);
    }

    private static bool IsTabPanel(UiState uiState, string panel) => DocumentPanels.FindTab(uiState.Documents, panel) != null;

    private static int WrapIndex(int index, int count)
    {
        var wrapped = index % count;
        if (wrapped < 0)
            wrapped += count;
        return wrapped;
    }

    public static void CloseActiveTab(UiState uiState)
    {
        var tab = uiState.ActiveTab;
        if (tab == null)
            return;
        CloseTab(uiState, tab);
    }

    /// <summary>Closes <paramref name="tab"/>'s panel, which asks for the tab to be closed.</summary>
    public static void CloseTab(UiState uiState, DocumentTab tab)
    {
        uiState.Layout.Close(DocumentPanels.GetPanel(uiState.Documents, tab));
    }

    /// <summary>Closes the document tabs of <paramref name="tab"/>'s group but <paramref name="tab"/>.</summary>
    public static void CloseOtherTabs(UiState uiState, DocumentTab tab)
    {
        var others = ListTabsInGroup(uiState, tab);
        others.Remove(tab);
        CloseTabs(uiState, others);
    }

    /// <summary>Closes every document tab of <paramref name="tab"/>'s group.</summary>
    public static void CloseTabsInGroup(UiState uiState, DocumentTab tab)
    {
        var all = ListTabsInGroup(uiState, tab);
        CloseTabs(uiState, all);
    }

    /// <summary>Closes the document tabs before <paramref name="tab"/> in its group.</summary>
    public static void CloseTabsLeftOf(UiState uiState, DocumentTab tab)
    {
        var tabs = ListTabsInGroup(uiState, tab);
        var index = tabs.IndexOf(tab);
        var left = tabs.GetRange(0, index);
        CloseTabs(uiState, left);
    }

    /// <summary>Closes the document tabs after <paramref name="tab"/> in its group.</summary>
    public static void CloseTabsRightOf(UiState uiState, DocumentTab tab)
    {
        var tabs = ListTabsInGroup(uiState, tab);
        var after = tabs.IndexOf(tab) + 1;
        var right = tabs.GetRange(after, tabs.Count - after);
        CloseTabs(uiState, right);
    }

    /// <summary>Closes the document tabs of every group, docked or floating.</summary>
    public static void CloseAllTabs(UiState uiState)
    {
        var all = new List<DocumentTab>();
        foreach (var group in uiState.Layout.AllGroups())
            AddTabsOf(uiState, all, group);
        CloseTabs(uiState, all);
    }

    /// <summary>The document tabs of <paramref name="tab"/>'s group, in the order its strip shows
    /// them, leaving out the other panels docked there.</summary>
    public static List<DocumentTab> ListTabsInGroup(UiState uiState, DocumentTab tab)
    {
        var tabs = new List<DocumentTab>();
        var group = uiState.Layout.GroupOf(DocumentPanels.GetPanel(uiState.Documents, tab));
        if (group == null)
            return tabs;
        AddTabsOf(uiState, tabs, group);
        return tabs;
    }

    /// <summary>Adds the document tabs of <paramref name="group"/> to <paramref name="tabs"/>, in the
    /// order its strip shows them.</summary>
    private static void AddTabsOf(UiState uiState, List<DocumentTab> tabs, DockGroup group)
    {
        foreach (var panel in group.Panels)
            AddTabOf(uiState, tabs, panel);
    }

    /// <summary>Adds the tab of <paramref name="panel"/> to <paramref name="tabs"/> when it is a document's.</summary>
    private static void AddTabOf(UiState uiState, List<DocumentTab> tabs, string? panel)
    {
        var tab = DocumentPanels.FindTab(uiState.Documents, panel);
        if (tab == null)
            return;
        tabs.Add(tab);
    }

    private static void CloseTabs(UiState uiState, List<DocumentTab> tabs)
    {
        foreach (var tab in tabs)
            CloseTab(uiState, tab);
    }

    /// <summary>The panel of the active tab, which the views dock beside, or null.</summary>
    internal static string? FindActivePanel(UiState uiState) => DocumentPanels.FindPanel(uiState.Documents, uiState.ActiveTab);

    /// <summary>Lets each view follow the grid or picture its tab shows.</summary>
    public static void FollowTabContent(UiState uiState)
    {
        DocumentPanels.FollowViewContent(uiState.Documents);
    }

    /// <summary>Drops the view of each tab closed, keeping its image to free, and closes its panel; and
    /// makes a view for each tab opened that has none yet.</summary>
    public static void SyncTabViews(UiState uiState, DocumentTabs tabs)
    {
        DropClosedTabViews(uiState, tabs);
        AddOpenTabViews(uiState, tabs);
    }

    private static void DropClosedTabViews(UiState uiState, DocumentTabs tabs)
    {
        var documents = uiState.Documents;
        foreach (var tab in DocumentPanels.ListClosedTabs(documents, tabs))
        {
            var panel = DocumentPanels.GetPanel(documents, tab);
            RemoveView(documents, tab);
            ClosePanel(uiState, panel);
        }
    }

    private static void ClosePanel(UiState uiState, string panel)
    {
        if (!uiState.Layout.Contains(panel))
            return;
        uiState.Layout.Close(panel);
    }

    private static void AddOpenTabViews(UiState uiState, DocumentTabs tabs)
    {
        for (var i = 0; i < tabs.Open.Count; i++)
            DocumentPanels.AddView(uiState.Documents, tabs.Open[i]);
    }

    /// <summary>The tab in front of each group that has one.</summary>
    public static List<DocumentTab> ListFrontTabs(UiState uiState)
    {
        var tabs = new List<DocumentTab>();
        foreach (var group in uiState.Layout.AllGroups())
            AddTabOf(uiState, tabs, group.Active);
        return tabs;
    }
}
