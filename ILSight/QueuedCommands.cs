using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;

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
            showTab: static (s, show) => ShowTabIfOpen(s.uiState, s.appState, show.Tab, show.Beside),
            showReopened: static (s, show) => ShowReopenedIfOpen(s.uiState, s.appState, show.Tabs, show.Layout),
            openPanel: static (s, open) => ScreenLayout.OpenPanel(s.uiState, open.View),
            restoreView: static (s, restore) => RestoreView(s.uiState, s.appState, restore.Tab, restore.Position),
            revealLine: static (s, reveal) => RevealLine(s.uiState, s.appState, reveal.Tab, reveal.Line),
            revealRow: static (s, reveal) => RevealRow(s.uiState, s.appState, reveal.Tab, reveal.Row),
            revealNode: static (s, reveal) => RevealIfListed(s.uiState, s.appState, reveal.Node),
            expandNodes: static (s, expand) => ExpandListed(s.uiState, s.appState, expand.Nodes),
            focusCard: static (s, focus) => FocusCard(s.uiState, s.appState, focus.Tab, focus.Type),
            showAnalysis: static (s, show) => ShowAnalysis(s.uiState, s.appState, show.Root),
            openWebPage: static (s, open) => s.uiState.WebPagesToOpen.Enqueue(open.Url),
            quit: static (s, _) => s.uiState.QuitAsked = true,
            openFile: static (s, open) => s.uiState.FilesToOpen.Enqueue(open.Path));
    }

    private static void ShowTabIfOpen(UiState uiState, AppState appState, DocumentTab tab, DocumentTab? beside)
    {
        if (!appState.Navigation.IsOpen(tab))
        {
            return;
        }

        DocumentPanels.ShowTab(uiState, tab, beside);
    }

    /// <summary>Shows those of the reopened <paramref name="tabs"/> still open where they were docked.</summary>
    private static void ShowReopenedIfOpen(UiState uiState, AppState appState, IReadOnlyList<ReopenedTab> tabs, DockLayout? layout)
    {
        var open = tabs
            .Where(r => appState.Navigation.IsOpen(r.Tab))
            .ToList();
        DocumentPanels.ShowReopenedTabs(uiState, open, layout);
    }

    private static void RevealIfListed(UiState uiState, AppState appState, BrowseNode node)
    {
        if (!appState.Assemblies.IsListed(node.Root.Assembly))
        {
            return;
        }

        uiState.Tree.RevealNode(node);
    }

    private static void ExpandListed(UiState uiState, AppState appState, IReadOnlyList<BrowseNode> nodes)
    {
        var listed = nodes
            .Where(n => appState.Assemblies.IsListed(n.Root.Assembly))
            .ToList();
        uiState.Tree.ExpandNodes(listed);
    }

    /// <summary>Expands <paramref name="root"/> and selects it, unless it was removed meanwhile.</summary>
    private static void ShowAnalysis(UiState uiState, AppState appState, AnalyzerSymbolNode root)
    {
        if (!appState.Analyzer.HasRoot(root))
        {
            return;
        }

        uiState.Analyzer.ShowRoot(root);
    }

    private static void RestoreView(UiState uiState, AppState appState, DocumentTab tab, ViewPosition position)
    {
        if (!appState.Navigation.IsOpen(tab))
        {
            return;
        }

        DocumentPanels.RestoreView(uiState.Documents, tab, position);
    }

    private static void RevealLine(UiState uiState, AppState appState, DocumentTab tab, int line)
    {
        if (!appState.Navigation.IsOpen(tab))
        {
            return;
        }

        DocumentPanels.RevealLine(uiState.Documents, tab, line);
    }

    private static void RevealRow(UiState uiState, AppState appState, DocumentTab tab, int row)
    {
        if (!appState.Navigation.IsOpen(tab))
        {
            return;
        }

        DocumentPanels.RevealRow(uiState.Documents, tab, row);
    }

    private static void FocusCard(UiState uiState, AppState appState, DocumentTab tab, CodeReference.Entity type)
    {
        if (!appState.Navigation.IsOpen(tab))
        {
            return;
        }

        DocumentPanels.FocusCard(uiState.Documents, tab, type);
    }

    /// <summary>Runs the commands <paramref name="uiState"/> queued for the app state, in the order queued, each a unit
    /// of work of its own: one that throws is reported, and the rest still run, as the draw that queued them already
    /// says their work has started.</summary>
    internal static void RunAppCommands(UiState uiState, AppState appState)
    {
        var commands = uiState.AppCommands;
        while (commands.TryDequeue(out var command))
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
    internal static void OpenTabIn(UiState uiState, AppState appState, string panel, bool focus)
    {
        if (!uiState.Layout.Contains(panel))
        {
            return;
        }

        var tab = appState.Navigation.OpenEmptyTab();
        uiState.Documents.AddView(tab, panel);
        var becomesActive = focus || uiState.Layout.Focused == panel;
        if (becomesActive)
        {
            Commands.StartDecompiling(appState, appState.Navigation.FocusTab(tab, appState.Decompiler.Setup));
        }
    }

    internal static void CloseTabAndFocusNext(UiState uiState, AppState appState, DocumentTab tab)
    {
        var closed = appState.Navigation.CloseTab(tab);
        if (!closed.WasActive)
        {
            return;
        }

        DocumentPanels.FocusNextTab(uiState, appState.Navigation, appState.Decompiler.Setup);
    }
}
