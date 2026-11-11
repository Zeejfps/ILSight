using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using static ILSight.Analysis;
using static ILSight.Diagrams;
using static ILSight.Docking;
using static ILSight.DocumentPanels;
using static ILSight.ScreenLayout;

namespace ILSight;

internal static partial class AppFrames
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
        switch (command)
        {
            case ViewCommand.ShowTab show:
                ShowTabIfOpen(uiState, appState, show.Tab, show.Beside);
                break;
            case ViewCommand.ShowReopened show:
                ShowReopenedIfOpen(uiState, appState, show.Tabs, show.Layout);
                break;
            case ViewCommand.OpenPanel open:
                OpenPanel(uiState, open.View);
                break;
            case ViewCommand.RevealNode reveal:
                RevealIfListed(uiState, appState, reveal.Node);
                break;
            case ViewCommand.ExpandNodes expand:
                ExpandListed(uiState, appState, expand.Nodes);
                break;
            case ViewCommand.ShowAnalysis show:
                ShowAnalysis(uiState, appState, show.Root);
                break;
            case ViewCommand.RestoreView restore:
                RestoreView(uiState, appState, restore.Tab, restore.Position);
                break;
            case ViewCommand.RevealLine reveal:
                RevealLine(uiState, appState, reveal.Tab, reveal.Line);
                break;
            case ViewCommand.RevealRow reveal:
                RevealRow(uiState, appState, reveal.Tab, reveal.Row);
                break;
            case ViewCommand.FocusCard focus:
                FocusCard(uiState, appState, focus.Tab, focus.Type);
                break;
            case ViewCommand.OpenWebPage open:
                uiState.WebPagesToOpen.Enqueue(open.Url);
                break;
            case ViewCommand.OpenFile open:
                uiState.FilesToOpen.Enqueue(open.Path);
                break;
            case ViewCommand.Quit:
                uiState.QuitAsked = true;
                break;
        }
    }

    private static void ShowTabIfOpen(UiState uiState, AppState appState, DocumentTab tab, DocumentTab? beside)
    {
        if (!Tabs.IsOpen(appState.Tabs, tab))
        {
            return;
        }

        ShowTab(uiState, tab, beside);
    }

    /// <summary>Shows those of the reopened <paramref name="tabs"/> still open where they were docked.</summary>
    private static void ShowReopenedIfOpen(UiState uiState, AppState appState, IReadOnlyList<ReopenedTab> tabs, DockLayout? layout)
    {
        var open = tabs
            .Where(r => Tabs.IsOpen(appState.Tabs, r.Tab))
            .ToList();
        ShowReopenedTabs(uiState, open, layout);
    }

    private static void RevealIfListed(UiState uiState, AppState appState, BrowseNode node)
    {
        if (!AssemblyListing.IsListed(appState.Assemblies, node.Root.Assembly))
        {
            return;
        }

        Trees.RevealNode(uiState.Tree, node);
    }

    private static void ExpandListed(UiState uiState, AppState appState, IReadOnlyList<BrowseNode> nodes)
    {
        var listed = nodes
            .Where(n => AssemblyListing.IsListed(appState.Assemblies, n.Root.Assembly))
            .ToList();
        Trees.ExpandNodes(uiState.Tree, listed);
    }

    /// <summary>Expands <paramref name="root"/> and selects it, unless it was removed meanwhile.</summary>
    private static void ShowAnalysis(UiState uiState, AppState appState, AnalyzerSymbolNode root)
    {
        if (!Analysis.HasRoot(appState.Analyzer, root))
        {
            return;
        }

        Analysis.ShowRoot(uiState.Analyzer, root);
    }

    private static void RestoreView(UiState uiState, AppState appState, DocumentTab tab, ViewPosition position)
    {
        if (!Tabs.IsOpen(appState.Tabs, tab))
        {
            return;
        }

        DocumentPanels.RestoreView(uiState.Documents, tab, position);
    }

    private static void RevealLine(UiState uiState, AppState appState, DocumentTab tab, int line)
    {
        if (!Tabs.IsOpen(appState.Tabs, tab))
        {
            return;
        }

        DocumentPanels.RevealLine(uiState.Documents, tab, line);
    }

    private static void RevealRow(UiState uiState, AppState appState, DocumentTab tab, int row)
    {
        if (!Tabs.IsOpen(appState.Tabs, tab))
        {
            return;
        }

        DocumentPanels.RevealRow(uiState.Documents, tab, row);
    }

    private static void FocusCard(UiState uiState, AppState appState, DocumentTab tab, CodeReference.Entity type)
    {
        if (!Tabs.IsOpen(appState.Tabs, tab))
        {
            return;
        }

        DocumentPanels.FocusCard(uiState.Documents, tab, type);
    }

    /// <summary>Runs the commands <paramref name="uiState"/> queued for the app state, in the order queued.</summary>
    internal static void RunAppCommands(UiState uiState, AppState appState)
    {
        var commands = uiState.AppCommands;
        while (commands.TryDequeue(out var command))
        {
            command(appState);
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

        var tab = OpenEmptyTab(appState);
        DocumentPanels.AddView(uiState.Documents, tab, panel);
        var becomesActive = focus || uiState.Layout.Focused == panel;
        if (becomesActive)
        {
            FocusTab(appState, tab);
        }
    }

    internal static void CloseTabAndFocusNext(UiState uiState, AppState appState, DocumentTab tab)
    {
        var wasActive = CloseTab(appState, tab);
        if (!wasActive)
        {
            return;
        }

        FocusNextTab(uiState);
    }
}
