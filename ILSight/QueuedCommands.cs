using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.MainArea;

namespace ILSight;

[Edge]
internal static class QueuedCommands
{
    /// <summary>Runs the commands the features queued for the view, in the order they were queued, dropping
    /// commands about a node, tab, line or row that is gone since.</summary>
    internal static void RunViewCommands(UiState uiState, AppState appState)
    {
        var commands = appState.ViewCommands;
        while (commands.TryDequeue(out var command))
        {
            Run(command, uiState, appState);
        }
    }

    private static void Run(ViewCommand command, UiState uiState, AppState appState)
    {
        command.Switch((uiState, appState),
            showTab: static (s, show) => ShowTabIfOpen(s.uiState.Panels, s.appState.ListSession.Navigation, show.Tab, show.Beside),
            showReopened: static (s, show) => ShowReopenedIfOpen(s.uiState.Panels, s.appState.ListSession.Navigation, show.Tabs, show.Layout),
            openPanel: static (s, open) => s.uiState.Panels.Open(open.View),
            restoreView: static (s, restore) => RestoreView(s.uiState.Documents, s.appState.ListSession.Navigation, restore.Tab, restore.Position),
            revealLine: static (s, reveal) => RevealLine(s.uiState.Documents, s.appState.ListSession.Navigation, reveal.Tab, reveal.Line),
            revealRow: static (s, reveal) => RevealRow(s.uiState.Documents, s.appState.ListSession.Navigation, reveal.Tab, reveal.Row),
            revealNode: static (s, reveal) => RevealIfListed(s.uiState.Tree, s.appState.ListSession.Assemblies, reveal.Node),
            expandNodes: static (s, expand) => ExpandListed(s.uiState.Tree, s.appState.ListSession.Assemblies, expand.Nodes),
            focusCard: static (s, focus) => FocusCard(s.uiState.Documents, s.appState.ListSession.Navigation, focus.Tab, focus.Type),
            showAnalysis: static (s, show) => ShowAnalysis(s.uiState.Analyzer, s.appState.ListSession.Analyzer, show.Root),
            openWebPage: static (s, open) => s.uiState.Shell.OpenWebPage(open.Url),
            quit: static (s, _) => s.uiState.Shell.Quit(),
            openFile: static (s, open) => s.uiState.Shell.OpenFile(open.Path));
    }

    private static void ShowTabIfOpen(Panels panels, Navigation navigation, DocumentTab tab, DocumentTab? beside)
    {
        if (!navigation.IsOpen(tab))
        {
            return;
        }

        panels.ShowTab(tab, beside);
    }

    private static void ShowReopenedIfOpen(Panels panels, Navigation navigation, IReadOnlyList<ReopenedTab> tabs, DockLayout? saved)
    {
        var open = tabs
            .Where(r => navigation.IsOpen(r.Tab))
            .ToList();
        panels.ShowReopenedTabs(open, saved);
    }

    private static void RevealIfListed(BrowseTree tree, AssemblyList assemblies, BrowseNode node)
    {
        if (!assemblies.IsListed(node.Root.Assembly))
        {
            return;
        }

        tree.RevealNode(node);
    }

    private static void ExpandListed(BrowseTree tree, AssemblyList assemblies, IReadOnlyList<BrowseNode> nodes)
    {
        var listed = nodes
            .Where(n => assemblies.IsListed(n.Root.Assembly))
            .ToList();
        tree.ExpandNodes(listed);
    }

    private static void ShowAnalysis(AnalyzerRows rows, AnalyzerTree analyzer, AnalyzerSymbolNode root)
    {
        if (!analyzer.HasRoot(root))
        {
            return;
        }

        rows.ShowRoot(root);
    }

    private static void RestoreView(DocumentViews documents, Navigation navigation, DocumentTab tab, ViewPosition position)
    {
        if (!navigation.IsOpen(tab))
        {
            return;
        }

        DocumentPanels.RestoreView(documents, tab, position);
    }

    private static void RevealLine(DocumentViews documents, Navigation navigation, DocumentTab tab, int line)
    {
        if (!navigation.IsOpen(tab))
        {
            return;
        }

        DocumentPanels.RevealLine(documents, tab, line);
    }

    private static void RevealRow(DocumentViews documents, Navigation navigation, DocumentTab tab, int row)
    {
        if (!navigation.IsOpen(tab))
        {
            return;
        }

        DocumentPanels.RevealRow(documents, tab, row);
    }

    private static void FocusCard(DocumentViews documents, Navigation navigation, DocumentTab tab, CodeReference.Entity type)
    {
        if (!navigation.IsOpen(tab))
        {
            return;
        }

        DocumentPanels.FocusCard(documents, tab, type);
    }

    /// <summary>Runs the commands <paramref name="commands"/> holds for the app state, in the order queued, each a unit
    /// of work of its own: one that throws is reported, and the rest still run, as the draw that queued them already
    /// says their work has started.</summary>
    internal static void RunAppCommands(AppCommandQueue commands, AppState appState)
    {
        while (commands.TryTake(out var command))
        {
            Run(command, appState);
        }
    }

    private static void Run(AppCommand command, AppState appState)
    {
        try
        {
            command(appState);
        }
        catch (Exception ex)
        {
            Errors.Report(appState, new AppProblem.Unexpected(ex));
        }
    }

    /// <summary>Opens an empty tab shown in <paramref name="panel"/>, unless the panel was closed meanwhile,
    /// and makes it the active tab when asked to or when the layout focuses its panel.</summary>
    internal static void OpenTabIn(MainLayout layout, DocumentViews documents, AppState appState, string panel, bool focus)
    {
        if (!layout.Dock.Contains(panel))
        {
            return;
        }

        var tab = appState.ListSession.Navigation.OpenEmptyTab();
        documents.AddView(tab, panel);
        var becomesActive = focus || layout.Dock.Focused == panel;
        if (becomesActive)
        {
            Commands.StartDecompiling(appState, appState.ListSession.Navigation.FocusTab(tab, appState.Decompiler.Setup));
        }
    }

    internal static void CloseTabAndFocusNext(MainLayout layout, DocumentViews documents, IAppCommandWriter commands, AppState appState, DocumentTab tab)
    {
        var closed = appState.ListSession.Navigation.CloseTab(tab);
        if (!closed.WasActive)
        {
            return;
        }

        DocumentPanels.FocusNextTab(layout, documents, commands, appState.ListSession.Navigation, appState.Decompiler.Setup);
    }
}
