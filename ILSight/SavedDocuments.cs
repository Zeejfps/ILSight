using System.Diagnostics;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.DiagramArea;
using ILSight.AssemblyArea;
using ILSight.DocumentViewArea;

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
        var reopened = new ReopenedTab { Tab = tab, Record = record, Version = tab.Version };
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
    public static void ReopenDocuments(SavedAssemblyLists lists, OpenListSession session)
    {
        session.Reopened.Tabs.Clear();
        var saved = lists.ActiveDocuments;
        if (saved is null)
        {
            session.Navigation.CloseDocuments();
            return;
        }
        ReopenTabs(session, saved);
    }

    private static void ReopenTabs(OpenListSession session, DocumentsRecord saved)
    {
        if (saved.Tabs.Count == 0)
        {
            session.Navigation.CloseDocuments();
            return;
        }
        session.Navigation.CloseAllTabs();
        var reopened = saved.Tabs.ConvertAll(r => ReopenTab(session, r));
        MakeSavedActive(session, reopened, saved.Active);
        MakeSavedPreview(session, reopened, saved.Preview);
        session.Navigation.ShowReopened(reopened, saved.Layout);
    }

    private static ReopenedTab ReopenTab(OpenListSession session, DocumentRecord record)
    {
        var tab = session.Navigation.OpenReopenedTab();
        WaitForSaved(session.Navigation, tab, record);
        return WaitFor(session.Reopened, tab, record);
    }

    private static void WaitForSaved(Navigation navigation, DocumentTab tab, DocumentRecord record)
    {
        switch (record)
        {
            case NodeDocumentRecord node:
                navigation.WaitForNode(tab, node.Location, node.Title, node.Language);
                break;
            case DiagramDocumentRecord diagram:
                navigation.WaitForDiagram(tab, diagram.Title);
                break;
            case ComparisonDocumentRecord comparison:
                navigation.WaitForComparison(tab, comparison.Title);
                break;
        }
    }

    /// <summary>Makes the tab reopened from the one docked as <paramref name="panel"/> active, else the first.</summary>
    private static void MakeSavedActive(OpenListSession session, List<ReopenedTab> reopened, string? panel)
    {
        var active = FindReopened(reopened, panel);
        if (active is null)
        {
            active = reopened[0];
        }

        session.Navigation.RestoreActiveTab(active.Tab);
    }

    private static void MakeSavedPreview(OpenListSession session, List<ReopenedTab> reopened, string? panel)
    {
        var preview = FindReopened(reopened, panel);
        if (preview is null)
        {
            return;
        }

        session.Navigation.RestorePreviewTab(preview.Tab);
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
    internal static DocumentsRecord? SaveDocuments(OpenListSession session, ViewSettings view)
    {
        var saved = session.Navigation
            .OpenTabs.Select(t => SaveTab(session, view, t))
            .OfType<SavedTab>()
            .ToList();
        if (saved.Count == 0)
        {
            return null;
        }

        var records = saved.ConvertAll(static s => s.Record);
        var active = FindSavedPanel(saved, session.Navigation.ActiveTab);
        var preview = FindSavedPanel(saved, session.Navigation.PreviewTab);
        var layout = ArrangeSaved(view.Layout, records);
        return new DocumentsRecord { Tabs = records, Active = active, Preview = preview, Layout = layout };
    }

    /// <summary>What the settings keep of <paramref name="tab"/>; null for one they leave out: one the screen docks
    /// nowhere, and one whose assembly was closed (<see cref="DocumentTab.ClosedAssembly"/>: a node's, a diagram's or a
    /// comparison's), gone after a restart.</summary>
    private static SavedTab? SaveTab(OpenListSession session, ViewSettings view, DocumentTab tab)
    {
        var panel = FindTabPanel(view, tab);
        var waiting = FindWaiting(session.Reopened, tab);
        if (waiting is not null)
        {
            return new SavedTab { Tab = tab, Record = KeepWaiting(waiting, panel) };
        }

        if (tab.ClosedAssembly is not null)
        {
            return null;
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

        return new SavedTab { Tab = tab, Record = record };
    }

    private static TabPanel? FindTabPanel(ViewSettings view, DocumentTab tab)
    {
        foreach (var panel in view.Tabs ?? [])
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

        return new NodeDocumentRecord
        {
            Panel = panel.Panel,
            Title = tab.Title,
            Location = location,
            Language = tab.Load.Language,
            View = tab.Load.PositionToReopen,
        };
    }

    private static DiagramDocumentRecord SaveDiagram(DocumentTab tab, TypeDiagram diagram, TabPanel panel)
    {
        var types = diagram.ListAnchors();
        var homes = diagram.HomeAssemblies.ToList();
        var view = panel.View as TabPanelView.Diagram;
        return new DiagramDocumentRecord
        {
            Panel = panel.Panel,
            Title = tab.Title,
            Types = types,
            HomeAssemblies = homes,
            View = view?.View,
        };
    }

    private static ComparisonDocumentRecord SaveComparison(DocumentTab tab, AssemblyComparison comparison, TabPanel panel)
    {
        var view = panel.View as TabPanelView.Comparison;
        return new ComparisonDocumentRecord
        {
            Panel = panel.Panel,
            Title = tab.Title,
            LeftPath = comparison.LeftPath,
            RightPath = comparison.RightPath,
            Scope = comparison.Scope,
            View = view?.View,
        };
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

    /// <summary>Shows again what the tabs of <paramref name="navigation"/> reopened and still waiting
    /// (<paramref name="reopenedTabs"/>) showed, now that the open list's assemblies, <paramref name="assemblies"/>, are
    /// listed: shows their diagrams and comparisons. Returns the loads that started in the tabs, for the edge that
    /// started this to start: the reading of the diagrams' cards, the comparisons' runs, and the lookup of the nodes the
    /// other tabs wait for (<see cref="ShowReopenedNodesHandler"/>), which nothing after this needs and which the edge
    /// starts as work of its own, so that what the caller started meanwhile does not wait for it.</summary>
    internal static TabLoads ShowReopenedDocuments(Navigation navigation, ReopenedTabs reopenedTabs, AssemblyList assemblies)
    {
        var nodes = new List<WaitingNode>();
        var diagramReads = new List<SavedDiagramRead>();
        var comparisonRuns = new List<ComparisonRun>();
        foreach (var reopened in ListWaiting(reopenedTabs))
        {
            ShowReopened(navigation, reopenedTabs, assemblies, reopened, nodes, diagramReads, comparisonRuns);
        }

        return TabLoads.From([], diagramReads, ListLookups(nodes), comparisonRuns);
    }

    /// <summary>Whether a tab whose assembly was closed (<see cref="DocumentTab.ClosedAssembly"/>: a node's, a diagram's
    /// or a comparison's) was read from a file the list holds again, read: opened, reloaded or restored, whichever way it
    /// came back. Allocates nothing.</summary>
    internal static bool HasClosedTabListedAgain(Navigation navigation, AssemblyList assemblies)
    {
        var tabs = navigation.OpenTabs;
        for (var i = 0; i < tabs.Count; i++)
        {
            if (IsListedAgain(tabs[i], assemblies))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsListedAgain(DocumentTab tab, AssemblyList assemblies)
    {
        var closed = tab.ClosedFile;
        return closed is not null && assemblies.IsListed(closed);
    }

    /// <summary>Revives each tab whose assembly was closed and is on the list again (<see cref="HasClosedTabListedAgain"/>),
    /// docked as <paramref name="documents"/> says: it waits for what it showed, as a tab reopened from the saved
    /// documents does, and shows it again as one does once the assemblies are listed
    /// (<see cref="ShowReopenedDocuments"/>): a node found again in what was read, back where its view stood, a diagram
    /// whose cards are read again, a comparison run again, the bar saying its assembly was closed gone. Returns those
    /// loads for the edge to start.</summary>
    internal static TabLoads ReviveClosedTabs(OpenListSession session, DocumentViews documents)
    {
        var nodes = new List<WaitingNode>();
        var diagramReads = new List<SavedDiagramRead>();
        var comparisonRuns = new List<ComparisonRun>();
        var tabs = session.Navigation.OpenTabs;
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            if (!IsListedAgain(tab, session.Assemblies))
            {
                continue;
            }

            var revived = Revive(session, documents, tab);
            if (revived is not null)
            {
                ShowReopened(session.Navigation, session.Reopened, session.Assemblies, revived, nodes, diagramReads, comparisonRuns);
            }
        }

        return TabLoads.From([], diagramReads, ListLookups(nodes), comparisonRuns);
    }

    /// <summary>Has <paramref name="tab"/>, whose assembly was closed and is listed again, wait for what it showed, kept
    /// as the settings would keep it; null while the screen docks it nowhere yet.</summary>
    private static ReopenedTab? Revive(OpenListSession session, DocumentViews documents, DocumentTab tab)
    {
        var panel = DocumentPanels.FindTabPanel(documents, tab);
        if (panel is null)
        {
            return null;
        }

        var record = SaveShown(tab, panel);
        if (record is null)
        {
            return null;
        }

        WaitForSaved(session.Navigation, tab, record);
        return WaitFor(session.Reopened, tab, record);
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
    private static void ShowReopened(Navigation navigation, ReopenedTabs reopenedTabs, AssemblyList assemblies, ReopenedTab reopened, List<WaitingNode> nodes,
        List<SavedDiagramRead> diagramReads, List<ComparisonRun> comparisonRuns)
    {
        switch (reopened.Record)
        {
            case NodeDocumentRecord node:
                nodes.Add(new WaitingNode { Reopened = reopened, Record = node, Place = FindListedPlace(assemblies, node) });
                break;
            case DiagramDocumentRecord diagram:
                AddStarted(diagramReads, Diagrams.ReopenDiagram(navigation, reopenedTabs, reopened, diagram));
                break;
            case ComparisonDocumentRecord comparison:
                AddStarted(comparisonRuns, Comparisons.ReopenComparison(navigation, reopenedTabs, assemblies, reopened, comparison));
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
        if (!assemblies.IsListed(node.Location.FilePath))
        {
            return null;
        }

        return node.Location;
    }

    private sealed record SavedTab
    {
        public required DocumentTab Tab { get; init; }

        public required DocumentRecord Record { get; init; }
    }
}
