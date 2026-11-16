using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.MainArea;
using ILSight.ILSightAppArea;

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

        documents.RestoreView(tab, position);
    }

    private static void RevealLine(DocumentViews documents, Navigation navigation, DocumentTab tab, int line)
    {
        if (!navigation.IsOpen(tab))
        {
            return;
        }

        documents.RevealLine(tab, line);
    }

    private static void RevealRow(DocumentViews documents, Navigation navigation, DocumentTab tab, int row)
    {
        if (!navigation.IsOpen(tab))
        {
            return;
        }

        documents.RevealRow(tab, row);
    }

    private static void FocusCard(DocumentViews documents, Navigation navigation, DocumentTab tab, CodeReference.Entity type)
    {
        if (!navigation.IsOpen(tab))
        {
            return;
        }

        documents.FocusCard(tab, type);
    }

    /// <summary>Runs the commands <paramref name="commands"/> holds for the app state, in the order queued, each a unit
    /// of work of its own: one that throws is reported, and the rest still run, as the draw that queued them already
    /// says their work has started.</summary>
    /// <summary>Runs what the screen asked of the app state (<see cref="RunAppCommands"/>), and asks
    /// <paramref name="frame"/> for another frame to show what it changed; nothing while none was asked.</summary>
    internal static void RunAppCommandsAndRedraw(IFrameHost frame, AppCommandQueue commands, AppState appState)
    {
        if (commands.IsEmpty)
        {
            return;
        }

        RunAppCommands(commands, appState);
        frame.RequestFrame();
    }

    /// <summary>Asks <paramref name="frame"/> for another frame while commands the features queued for the screen
    /// wait in <paramref name="viewCommands"/>.</summary>
    internal static void RequestFrameWhileViewCommandsWait(IFrameHost frame, Queue<ViewCommand> viewCommands)
    {
        if (viewCommands.Count == 0)
        {
            return;
        }

        frame.RequestFrame();
    }

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
    internal static void OpenTabIn(Panels panels, AppState appState, string panel, bool focus)
    {
        var navigation = appState.ListSession.Navigation;
        var tab = panels.OpenTabIn(navigation, panel, focus);
        if (tab is null)
        {
            return;
        }

        var decompileTicket = navigation.FocusTab(tab, appState.Decompiler.Setup);
        Commands.StartDecompiling(appState, decompileTicket);
    }

    internal static void CloseTabAndFocusNext(Panels panels, AppState appState, DocumentTab tab)
    {
        var closed = appState.ListSession.Navigation.CloseTab(tab);
        if (!closed.WasActive)
        {
            return;
        }

        panels.FocusNextTab(appState.ListSession.Navigation, appState.Decompiler.Setup);
    }
}
