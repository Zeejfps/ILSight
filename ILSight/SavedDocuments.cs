using System.Diagnostics;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Diagramming;
using static ILSight.Comparisons;
using static ILSight.Decompilation;
using static ILSight.Diagramming.Diagrams;
using static ILSight.Docking;

namespace ILSight;

/// <summary>The documents each assembly list keeps open between runs: keeping the open tabs with the
/// open list, reopening a list's tabs as it opens, and showing what they showed again once its assemblies
/// are listed.</summary>
public static class SavedDocuments
{
    /// <summary>Has <paramref name="tab"/>, as it is now, wait for what <paramref name="record"/> says it showed.</summary>
    public static ReopenedTab WaitFor(ReopenedTabs tabs, DocumentTab tab, DocumentRecord record)
    {
        tabs.Tabs.RemoveAll(r => r.Tab == tab);
        var reopened = new ReopenedTab(tab, record, tab.Version);
        tabs.Tabs.Add(reopened);
        return reopened;
    }

    /// <summary>What the settings kept of <paramref name="tab"/> while it still waits for it; else null.</summary>
    public static DocumentRecord? FindWaiting(ReopenedTabs tabs, DocumentTab tab)
    {
        foreach (var reopened in tabs.Tabs)
        {
            if (reopened.Tab == tab && reopened.IsWaiting)
            {
                return reopened.Record;
            }
        }
        return null;
    }

    /// <summary>The tabs still waiting, in the order reopened.</summary>
    public static List<ReopenedTab> ListWaiting(ReopenedTabs tabs)
    {
        return tabs.Tabs
            .Where(static r => r.IsWaiting)
            .ToList();
    }

    /// <summary>Stops <paramref name="reopened"/> waiting. False when it waited no more, as when its tab
    /// was pointed elsewhere or another reopening took its place, and what it found should be dropped.</summary>
    public static bool TryFinishWaiting(ReopenedTabs tabs, ReopenedTab reopened)
    {
        if (!tabs.Tabs.Remove(reopened))
        {
            return false;
        }

        return reopened.IsWaiting;
    }

    /// <summary>Opens the documents the open list kept, in place of the open tabs, each waiting for what it
    /// showed until the list's assemblies are listed; makes the one that was active and the one that was
    /// the preview tab so again; and commands the view to dock them where they were. With none kept, closes
    /// every tab but the active one, which then shows nothing.</summary>
    public static void ReopenDocuments(AppState appState)
    {
        appState.Reopened.Tabs.Clear();
        var saved = appState.Lists.ActiveDocuments;
        if (saved is null)
        {
            CloseDocuments(appState);
            return;
        }
        ReopenTabs(appState, saved);
    }

    private static void ReopenTabs(AppState appState, DocumentsRecord saved)
    {
        if (saved.Tabs.Count == 0)
        {
            CloseDocuments(appState);
            return;
        }
        CloseAllTabs(appState);
        var reopened = saved.Tabs.ConvertAll(r => ReopenTab(appState, r));
        MakeSavedActive(appState, reopened, saved.Active);
        MakeSavedPreview(appState, reopened, saved.Preview);
        appState.ViewCommands.Enqueue(new ViewCommand.ShowReopened(reopened, saved.Layout));
    }

    private static ReopenedTab ReopenTab(AppState appState, DocumentRecord record)
    {
        var tab = appState.Tabs.AddNew();
        WaitForSaved(tab, record);
        return WaitFor(appState.Reopened, tab, record);
    }

    private static void WaitForSaved(DocumentTab tab, DocumentRecord record)
    {
        switch (record)
        {
            case NodeDocumentRecord node:
                Tabs.WaitForNode(tab, node.Location, node.Title, node.Language);
                break;
            case DiagramDocumentRecord diagram:
                Tabs.WaitForDiagram(tab, diagram.Title);
                break;
            case ComparisonDocumentRecord comparison:
                Tabs.WaitForComparison(tab, comparison.Title);
                break;
        }
    }

    /// <summary>Makes the tab reopened from the one docked as <paramref name="panel"/> active, else the first.</summary>
    private static void MakeSavedActive(AppState appState, List<ReopenedTab> reopened, string? panel)
    {
        var active = FindReopened(reopened, panel);
        if (active is null)
        {
            active = reopened[0];
        }

        Tabs.MakeActive(appState.Tabs, active.Tab);
    }

    private static void MakeSavedPreview(AppState appState, List<ReopenedTab> reopened, string? panel)
    {
        var preview = FindReopened(reopened, panel);
        if (preview is null)
        {
            return;
        }

        Tabs.MakePreview(appState.Tabs, preview.Tab);
    }

    private static ReopenedTab? FindReopened(List<ReopenedTab> reopened, string? panel)
    {
        if (panel is null)
        {
            return null;
        }

        return reopened.Find(r => r.Record.Panel == panel);
    }

    /// <summary>The open tabs as the settings keep them with the open list: each docked tab showing a node
    /// or a diagram, and each still waiting for what it was reopened to show, with the active and the
    /// preview tab and how their panels are arranged. Null when no tab is kept.</summary>
    internal static DocumentsRecord? SaveDocuments(AppState appState)
    {
        var saved = appState.Tabs
            .Open.Select(t => SaveTab(appState, t))
            .OfType<SavedTab>()
            .ToList();
        if (saved.Count == 0)
        {
            return null;
        }

        var records = saved.ConvertAll(static s => s.Record);
        var active = FindSavedPanel(saved, appState.Tabs.Active);
        var preview = FindSavedPanel(saved, appState.Tabs.Preview);
        var layout = ArrangeSaved(appState.View.Layout, records);
        return new DocumentsRecord(records, active, preview, layout);
    }

    private static SavedTab? SaveTab(AppState appState, DocumentTab tab)
    {
        var panel = FindTabPanel(appState, tab);
        var waiting = FindWaiting(appState.Reopened, tab);
        if (waiting is not null)
        {
            return new SavedTab(tab, KeepWaiting(waiting, panel));
        }

        if (panel is null)
        {
            return null;
        }

        var record = SaveShown(tab, panel);
        if (record is null)
        {
            return null;
        }

        return new SavedTab(tab, record);
    }

    private static TabPanel? FindTabPanel(AppState appState, DocumentTab tab)
    {
        foreach (var panel in appState.View.Tabs ?? [])
        {
            if (panel.Tab == tab)
            {
                return panel;
            }
        }
        return null;
    }

    /// <summary>What a tab still waiting was saved as, docked as <paramref name="panel"/> says when the
    /// screen docked it.</summary>
    private static DocumentRecord KeepWaiting(DocumentRecord waiting, TabPanel? panel)
    {
        if (panel is null)
        {
            return waiting;
        }

        return waiting with { Panel = panel.Panel };
    }

    /// <summary>What the settings keep of a tab the screen shows: its diagram, its comparison, or the node at
    /// its location; null for a tab showing none of these, such as a report or nothing.</summary>
    private static DocumentRecord? SaveShown(DocumentTab tab, TabPanel panel)
    {
        var diagram = tab.Diagram;
        if (diagram is not null)
        {
            return SaveDiagram(tab, diagram, panel);
        }

        var comparison = tab.Comparison;
        if (comparison is not null)
        {
            return SaveComparison(tab, comparison, panel);
        }

        var location = tab.Location;
        if (location is null)
        {
            return null;
        }

        return new NodeDocumentRecord(panel.Panel, tab.Title, location, tab.Language, tab.PositionToReopen);
    }

    private static DiagramDocumentRecord SaveDiagram(DocumentTab tab, TypeDiagram diagram, TabPanel panel)
    {
        var types = diagram.Cards
            .Select(static c => c.Anchor)
            .ToList();
        var homes = diagram.HomeAssemblies.ToList();
        var view = panel.View as TabPanelView.Diagram;
        return new DiagramDocumentRecord(panel.Panel, tab.Title, types, homes, view?.View);
    }

    private static ComparisonDocumentRecord SaveComparison(DocumentTab tab, AssemblyComparison comparison, TabPanel panel)
    {
        var view = panel.View as TabPanelView.Comparison;
        return new ComparisonDocumentRecord(panel.Panel, tab.Title, comparison.LeftPath, comparison.RightPath, comparison.Scope,
            view?.View);
    }

    private static string? FindSavedPanel(List<SavedTab> saved, DocumentTab? tab)
    {
        var found = saved.Find(s => s.Tab == tab);
        if (found is null)
        {
            return null;
        }

        return found.Record.Panel;
    }

    /// <summary>A copy of <paramref name="layout"/> holding the panels of <paramref name="records"/> alone,
    /// as they are arranged in it; null when there is no layout.</summary>
    private static DockLayout? ArrangeSaved(DockLayout? layout, List<DocumentRecord> records)
    {
        if (layout is null)
        {
            return null;
        }

        var panels = records.ConvertAll(static r => r.Panel);
        var arrangement = layout.CopyArrangement();
        arrangement.Retain(panels.Contains);
        return arrangement;
    }

    /// <summary>Shows again what the tabs of <paramref name="tabs"/> reopened and still waiting
    /// (<paramref name="reopenedTabs"/>) showed, now that the open list's assemblies, <paramref name="assemblies"/>, are
    /// listed: shows their diagrams and comparisons. Returns the loads that started in the tabs, for the edge that
    /// started this to start: the reading of the diagrams' cards, the comparisons' runs, and the lookup of the nodes the
    /// other tabs wait for (<see cref="ShowReopenedNodesHandler"/>), which nothing after this needs and which the edge
    /// starts as work of its own, so that what the caller started meanwhile does not wait for it.</summary>
    internal static TabLoads ShowReopenedDocuments(DocumentTabs tabs, ReopenedTabs reopenedTabs, AssemblyList assemblies)
    {
        var nodes = new List<WaitingNode>();
        var diagramReads = new List<SavedDiagramRead>();
        var comparisonRuns = new List<ComparisonRun>();
        foreach (var reopened in ListWaiting(reopenedTabs))
        {
            ShowReopened(tabs, reopenedTabs, assemblies, reopened, nodes, diagramReads, comparisonRuns);
        }

        return TabLoads.From([], diagramReads, ListLookups(nodes), comparisonRuns);
    }

    /// <summary>The lookup of <paramref name="nodes"/>; none when no tab waits for a node.</summary>
    private static List<ReopenedNodesLookup> ListLookups(List<WaitingNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return [];
        }

        return [new ReopenedNodesLookup { Nodes = nodes }];
    }

    /// <summary>Shows the diagram or comparison <paramref name="reopened"/> showed, adding the read of its cards to
    /// <paramref name="diagramReads"/>, or its run to <paramref name="comparisonRuns"/>, or adds its node to
    /// <paramref name="nodes"/>, to be found with the others.</summary>
    private static void ShowReopened(DocumentTabs tabs, ReopenedTabs reopenedTabs, AssemblyList assemblies, ReopenedTab reopened, List<WaitingNode> nodes,
        List<SavedDiagramRead> diagramReads, List<ComparisonRun> comparisonRuns)
    {
        switch (reopened.Record)
        {
            case NodeDocumentRecord node:
                nodes.Add(new WaitingNode { Reopened = reopened, Record = node, Place = FindListedPlace(assemblies, node) });
                break;
            case DiagramDocumentRecord diagram:
                AddStarted(diagramReads, ReopenDiagram(tabs, reopenedTabs, reopened, diagram));
                break;
            case ComparisonDocumentRecord comparison:
                AddStarted(comparisonRuns, ReopenComparison(reopenedTabs, assemblies, reopened, comparison));
                break;
        }
    }

    /// <summary>Adds <paramref name="work"/> to <paramref name="started"/> when a reopening started one.</summary>
    private static void AddStarted<T>(List<T> started, T? work) where T : class
    {
        if (work is null)
        {
            return;
        }

        started.Add(work);
    }

    /// <summary>Where <paramref name="node"/> was, when its file is open in the list; else null, as the
    /// list holds it no more.</summary>
    private static NodePath? FindListedPlace(AssemblyList assemblies, NodeDocumentRecord node)
    {
        if (!AssemblyListing.IsListed(assemblies, node.Location.FilePath))
        {
            return null;
        }

        return node.Location;
    }

    private sealed record SavedTab(DocumentTab Tab, DocumentRecord Record);
}
