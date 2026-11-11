using System.Diagnostics;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using static ILSight.Decompilation;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>Opening empty document tabs, switching to them and closing them. Where their panels are
/// docked is the screen's.</summary>
public static class Docking
{
    /// <summary>Opens an empty tab for a panel the screen made for itself.</summary>
    public static DocumentTab OpenEmptyTab(AppState appState)
    {
        return Tabs.AddNew(appState.Tabs);
    }

    /// <summary>Makes <paramref name="tab"/>, when it is open, the tab the user works in, decompiling
    /// again what it shows when the language or the options changed since.</summary>
    public static void FocusTab(AppState appState, DocumentTab tab)
    {
        if (!Tabs.IsOpen(appState.Tabs, tab))
        {
            return;
        }

        appState.Intent.Begin();
        Tabs.MakeActive(appState.Tabs, tab);
        DecompileAgainIfOutdated(appState, tab);
    }

    /// <summary>Closes <paramref name="tab"/>, cancelling what it was doing. Returns whether it was the
    /// active tab, which leaves none active until the view focuses another.</summary>
    public static bool CloseTab(AppState appState, DocumentTab tab)
    {
        if (!Tabs.IsOpen(appState.Tabs, tab))
        {
            return false;
        }

        appState.Intent.Begin();
        var wasActive = tab == appState.Tabs.Active;
        ForgetDiagramOf(appState, tab);
        Tabs.Remove(appState.Tabs, tab);
        return wasActive;
    }

    /// <summary>Drops the diagram <paramref name="tab"/> shows, which it is about to stop showing, from
    /// the history.</summary>
    private static void ForgetDiagramOf(AppState appState, DocumentTab tab)
    {
        var diagram = tab.Diagram;
        if (diagram == null)
        {
            return;
        }

        Navigation.ForgetDiagram(appState.History, diagram);
    }

    /// <summary>Closes every document tab but the active one, and empties that one; with no tab left
    /// open, opens an empty one.</summary>
    public static void CloseDocuments(AppState appState)
    {
        var kept = FindTabToKeep(appState);
        var others = appState.Tabs
            .Open.Where(t => t != kept)
            .ToList();
        foreach (var tab in others)
        {
            CloseTab(appState, tab);
        }

        ForgetDiagramOf(appState, kept);
        Tabs.Clear(kept);
        Tabs.MakeActive(appState.Tabs, kept);
    }

    private static DocumentTab FindTabToKeep(AppState appState)
    {
        var active = appState.Tabs.Active;
        if (active != null)
        {
            return active;
        }

        if (appState.Tabs.Open.Count > 0)
        {
            return appState.Tabs.Open[0];
        }

        return OpenNewTab(appState);
    }

    /// <summary>Closes every document tab, leaving none open.</summary>
    internal static void CloseAllTabs(AppState appState)
    {
        var all = appState.Tabs.Open.ToList();
        foreach (var tab in all)
        {
            CloseTab(appState, tab);
        }
    }

    private const float ToolShare = 0.3f;
    private const float DocumentShare = 0.66f;
    private const float AssembliesShare = 0.34f;

    /// <summary>Docks the analyzer beside the search results or bookmarks, or below the documents,
    /// if it is not docked, and brings it to the front.</summary>
    public static void OpenAnalyzer(MainLayout layout, string? activeDocument)
    {
        if (!layout.Dock.Contains(DockPanels.Analyzer))
        {
            DockAnalyzer(layout, activeDocument);
        }

        layout.Dock.Activate(DockPanels.Analyzer);
    }

    private static void DockAnalyzer(MainLayout layout, string? activeDocument)
    {
        var beside = FindSearchOrBookmarksGroup(layout);
        if (beside != null)
        {
            layout.Dock.Dock(DockPanels.Analyzer, beside, DockEdge.Center);
            return;
        }
        DockBelowDocuments(layout, DockPanels.Analyzer, activeDocument);
    }

    private static DockGroup? FindSearchOrBookmarksGroup(MainLayout layout)
    {
        var search = layout.Dock.GroupOf(DockPanels.Search);
        if (search != null)
        {
            return search;
        }

        return layout.Dock.GroupOf(DockPanels.Bookmarks);
    }

    /// <summary>Docks the decompiler options beside the documents if they are not, and brings them to the front.</summary>
    public static void OpenDecompilerOptions(MainLayout layout, string? activeDocument)
    {
        if (!layout.Dock.Contains(DockPanels.DecompilerOptions))
        {
            DockBeside(layout, DockPanels.DecompilerOptions, FindDocumentArea(layout, activeDocument), DockEdge.Right, ToolShare);
        }

        layout.Dock.Activate(DockPanels.DecompilerOptions);
    }

    /// <summary>Docks the display options beside the decompiler options, or beside the documents, if
    /// they are not, and brings them to the front.</summary>
    public static void OpenDisplayOptions(MainLayout layout, string? activeDocument)
    {
        if (!layout.Dock.Contains(DockPanels.DisplayOptions))
        {
            DockDisplayOptions(layout, activeDocument);
        }

        layout.Dock.Activate(DockPanels.DisplayOptions);
    }

    private static void DockDisplayOptions(MainLayout layout, string? activeDocument)
    {
        var decompilerOptions = layout.Dock.GroupOf(DockPanels.DecompilerOptions);
        if (decompilerOptions != null)
        {
            layout.Dock.Dock(DockPanels.DisplayOptions, decompilerOptions, DockEdge.Center);
            return;
        }
        DockBeside(layout, DockPanels.DisplayOptions, FindDocumentArea(layout, activeDocument), DockEdge.Right, ToolShare);
    }

    /// <summary>Docks the bookmarks panel if it is not, and brings it to the front.</summary>
    public static void OpenBookmarks(MainLayout layout, string? activeDocument)
    {
        if (!layout.Dock.Contains(DockPanels.Bookmarks))
        {
            DockBookmarks(layout, activeDocument);
        }

        layout.Dock.Activate(DockPanels.Bookmarks);
    }

    private static void DockBookmarks(MainLayout layout, string? activeDocument)
    {
        var search = layout.Dock.GroupOf(DockPanels.Search);
        if (search != null)
        {
            layout.Dock.Dock(DockPanels.Bookmarks, search, DockEdge.Center);
            return;
        }
        DockBelowDocuments(layout, DockPanels.Bookmarks, activeDocument);
    }

    /// <summary>Docks the exports panel beside the search results, bookmarks or analyzer, or below
    /// the documents, if it is not docked, and brings it to the front.</summary>
    public static void OpenExports(MainLayout layout, string? activeDocument)
    {
        if (!layout.Dock.Contains(DockPanels.Exports))
        {
            DockExports(layout, activeDocument);
        }

        layout.Dock.Activate(DockPanels.Exports);
    }

    private static void DockExports(MainLayout layout, string? activeDocument)
    {
        var beside = FindToolGroup(layout);
        if (beside != null)
        {
            layout.Dock.Dock(DockPanels.Exports, beside, DockEdge.Center);
            return;
        }
        DockBelowDocuments(layout, DockPanels.Exports, activeDocument);
    }

    private static DockGroup? FindToolGroup(MainLayout layout)
    {
        var group = FindSearchOrBookmarksGroup(layout);
        if (group != null)
        {
            return group;
        }

        return layout.Dock.GroupOf(DockPanels.Analyzer);
    }

    /// <summary>Docks the search panel under the documents if it is closed, and brings it to the front.</summary>
    public static void OpenSearch(MainLayout layout, string? activeDocument)
    {
        if (!layout.Dock.Contains(DockPanels.Search))
        {
            DockBelowDocuments(layout, DockPanels.Search, activeDocument);
        }

        layout.Dock.Activate(DockPanels.Search);
    }

    /// <summary>Docks the assemblies view where it starts out, left of everything, if it is closed,
    /// and brings it to the front.</summary>
    public static void OpenAssemblies(MainLayout layout)
    {
        if (!layout.Dock.Contains(DockPanels.Assemblies))
        {
            DockBeside(layout, DockPanels.Assemblies, layout.Dock.Root, DockEdge.Left, AssembliesShare);
        }

        layout.Dock.Activate(DockPanels.Assemblies);
    }

    /// <summary>Docks a new document into the documents' group, or where documents start out, right
    /// of everything, when there is none.</summary>
    public static void DockDocument(MainLayout layout, string panel, string? activeDocument)
    {
        var group = FindDocumentGroup(layout, activeDocument);
        if (group != null)
        {
            layout.Dock.Dock(panel, group, DockEdge.Center);
            return;
        }
        DockBeside(layout, panel, layout.Dock.Root, DockEdge.Right, DocumentShare);
    }

    /// <summary>Docks <paramref name="panels"/>, documents not docked yet, as <paramref name="arrangement"/>,
    /// a layout of documents named as they are, has them: its first group joins the documents' group, or
    /// goes where documents start out when there is none, the groups it splits off go beside that as it
    /// splits them, and its floating windows float again. Those it does not hold join the documents' group.</summary>
    public static void DockArranged(MainLayout layout, IReadOnlyList<string> panels, DockLayout arrangement)
    {
        var documents = FindDocumentGroup(layout, null);
        DockArranged(layout, arrangement.Root, documents, DockEdge.Center, DocumentShare);
        foreach (var window in arrangement.Windows)
        {
            FloatArranged(layout, window);
        }

        foreach (var panel in panels)
        {
            DockIfUndocked(layout, panel);
        }
    }

    /// <summary>Docks the documents under <paramref name="node"/> as it arranges them: its lead group into or
    /// beside <paramref name="target"/> as <paramref name="edge"/> says, or where documents start out when
    /// there is no target, then the others in the place that lead group takes.</summary>
    private static void DockArranged(MainLayout layout, DockNode node, DockGroup? target, DockEdge edge, float share)
    {
        var lead = FindLeadGroup(node);
        if (lead == null)
        {
            return;
        }

        var docked = DockGroupArranged(layout, lead, target, edge, share);
        if (docked == null)
        {
            return;
        }

        DockRestArranged(layout, node, docked);
    }

    /// <summary>Docks the groups under <paramref name="node"/> but its lead, docked as <paramref name="lead"/>,
    /// splitting the place it takes as <paramref name="node"/> splits it: each split's second side goes beside
    /// its first while that first side still fills the place, so each side then splits only its own part.</summary>
    private static void DockRestArranged(MainLayout layout, DockNode node, DockGroup lead)
    {
        if (node is not DockSplit split)
        {
            return;
        }

        if (FindLeadGroup(split.First) == null)
        {
            DockRestArranged(layout, split.Second, lead);
            return;
        }
        var second = FindLeadGroup(split.Second);
        if (second == null)
        {
            DockRestArranged(layout, split.First, lead);
            return;
        }
        var secondDocked = DockGroupArranged(layout, second, lead, GetEdgeAfter(split.Direction), 1f - split.Ratio);
        DockRestArranged(layout, split.First, lead);
        if (secondDocked != null)
        {
            DockRestArranged(layout, split.Second, secondDocked);
        }
    }

    /// <summary>The first group under <paramref name="node"/> that holds a document, or null when none does.</summary>
    private static DockGroup? FindLeadGroup(DockNode node)
    {
        if (node is DockGroup group)
        {
            return group.Panels.Count > 0 ? group : null;
        }

        if (node is not DockSplit split)
        {
            return null;
        }

        var first = FindLeadGroup(split.First);
        if (first != null)
        {
            return first;
        }

        return FindLeadGroup(split.Second);
    }

    private static DockGroup? DockGroupArranged(MainLayout layout, DockGroup group, DockGroup? target, DockEdge edge, float share)
    {
        if (group.Panels.Count == 0)
        {
            return null;
        }

        var first = group.Panels[0];
        DockFirstArranged(layout, first, target, edge, share);
        var docked = layout.Dock.GroupOf(first);
        if (docked == null)
        {
            return null;
        }

        foreach (var panel in group.Panels.Skip(1))
        {
            layout.Dock.Dock(panel, docked, DockEdge.Center);
        }

        BringToFront(layout, group.Active);
        return docked;
    }

    /// <summary>Docks <paramref name="panel"/>, the first of a group arranged, into or beside
    /// <paramref name="target"/>, or where documents start out when there is none; unless it is docked
    /// already, as the first panel of a floating window is.</summary>
    private static void DockFirstArranged(MainLayout layout, string panel, DockGroup? target, DockEdge edge, float share)
    {
        if (layout.Dock.Contains(panel))
        {
            return;
        }

        if (target == null)
        {
            DockBeside(layout, panel, layout.Dock.Root, DockEdge.Right, DocumentShare);
            return;
        }
        layout.Dock.Dock(panel, target, edge, share);
    }

    private static void BringToFront(MainLayout layout, string? panel)
    {
        if (panel == null)
        {
            return;
        }

        layout.Dock.Activate(panel);
    }

    private static DockEdge GetEdgeAfter(Direction direction)
    {
        return direction switch
        {
            Direction.Horizontal => DockEdge.Right,
            Direction.Vertical => DockEdge.Bottom,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Floats the documents of <paramref name="window"/> in a window of their own where it was,
    /// arranged as it arranges them.</summary>
    private static void FloatArranged(MainLayout layout, DockWindow window)
    {
        var panels = DockLayout.PanelsIn(window.Root);
        if (panels.Count == 0)
        {
            return;
        }

        layout.Dock.Float(panels[0], window.Rect);
        DockArranged(layout, window.Root, null, DockEdge.Center, DocumentShare);
    }

    private static void DockIfUndocked(MainLayout layout, string panel)
    {
        if (layout.Dock.Contains(panel))
        {
            return;
        }

        DockDocument(layout, panel, null);
    }

    public static List<string> ListDocumentPanels(MainLayout layout)
    {
        return layout.Dock.AllGroups()
            .SelectMany(g => g.Panels)
            .Where(IsDocumentPanel)
            .ToList();
    }

    /// <summary>Puts the layout back as it starts out: the assemblies left of <paramref name="documents"/>,
    /// tabbed together in one group. Floating windows and the other views close.</summary>
    public static void ResetLayout(MainLayout layout, List<string> documents)
    {
        layout.Dock.Windows.Clear();
        layout.Dock.Root = CreateAssembliesGroup(layout);
        DockDocumentsTogether(layout, documents);
    }

    private static DockGroup CreateAssembliesGroup(MainLayout layout)
    {
        var id = layout.Dock.NextId;
        layout.Dock.NextId++;
        return new DockGroup
        {
            Id = id,
            Panels = { DockPanels.Assemblies },
            Active = DockPanels.Assemblies,
        };
    }

    private static void DockDocumentsTogether(MainLayout layout, List<string> documents)
    {
        var first = documents[0];
        DockBeside(layout, first, layout.Dock.Root, DockEdge.Right, DocumentShare);
        var group = layout.Dock.GroupOf(first);
        if (group == null)
        {
            throw new UnreachableException($"{first}, just docked, is in no group.");
        }

        foreach (var panel in documents.Skip(1))
        {
            layout.Dock.Dock(panel, group, DockEdge.Center);
        }
    }

    private static void DockBelowDocuments(MainLayout layout, string panel, string? activeDocument)
    {
        DockBeside(layout, panel, FindDocumentArea(layout, activeDocument), DockEdge.Bottom, ToolShare);
    }

    private static DockNode FindDocumentArea(MainLayout layout, string? activeDocument)
    {
        var group = FindDocumentGroup(layout, activeDocument);
        if (group != null)
        {
            return group;
        }

        return layout.Dock.Root;
    }

    /// <summary>The group a new document joins: the active document's, else the first holding one.</summary>
    private static DockGroup? FindDocumentGroup(MainLayout layout, string? activeDocument)
    {
        var active = FindActiveGroup(layout, activeDocument);
        if (active != null)
        {
            return active;
        }

        foreach (var group in layout.Dock.AllGroups())
        {
            if (group.Panels.Exists(IsDocumentPanel))
            {
                return group;
            }
        }
        return null;
    }

    private static DockGroup? FindActiveGroup(MainLayout layout, string? activeDocument)
    {
        if (activeDocument == null)
        {
            return null;
        }

        return layout.Dock.GroupOf(activeDocument);
    }

    /// <summary>Docks <paramref name="panel"/> beside <paramref name="target"/>, or into it when the
    /// layout is empty.</summary>
    private static void DockBeside(MainLayout layout, string panel, DockNode target, DockEdge edge, float share)
    {
        var root = layout.Dock.Root as DockGroup;
        if (root != null && root.Panels.Count == 0)
        {
            layout.Dock.Dock(panel, root, DockEdge.Center);
            return;
        }
        layout.Dock.Dock(panel, target, edge, share);
    }

    private static bool IsDocumentPanel(string panel)
    {
        return !DockPanels.IsView(panel);
    }
}
