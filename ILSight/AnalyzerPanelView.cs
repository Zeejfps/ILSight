using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Analysis;
using static ILSight.Analysis;
using static ILSight.Navigation;
using static ILSight.ScreenLayout;

namespace ILSight;

internal static class AnalyzerPanelView
{
    private const string TreeId = "analyzer";
    private const string MenuId = "analyzer-menu";

    public static void Render(Ui<UiState> ui, AnalyzerTree analyzer)
    {
        var t = ui.State.Strings;
        var rows = ui.State.Analyzer;
        Analysis.SetRowStyle(rows, ui.Theme, ui.State.NodeIconPalette, ui.State.Icons, t);
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        Toolbar(ui, analyzer, rows);
        ui.Separator();
        if (rows.Roots.Count == 0)
        {
            DocumentsView.Hint(ui, t.AnalyzerHint(Shortcuts.Analyze.Label));
            return;
        }
        Tree(ui);
        ContextMenu(ui, analyzer);
    }

    private static void Toolbar(Ui<UiState> ui, AnalyzerTree analyzer, AnalyzerRows rows)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var any = analyzer.Roots.Count > 0;
        var root = Analysis.FindSelectedRoot(rows);
        using (ui.PaneToolbar())
        {
            if (ui.IconButton("collapse", ui.State.Icons.AnalyzerCollapseAll, t.CodeCollapseAll, any))
            {
                Analysis.CollapseAllRows(rows);
            }

            if (ui.IconButton("expand", ui.State.Icons.AnalyzerExpandAll, t.CodeExpandAll, any))
            {
                Analysis.ExpandAllRows(rows);
            }

            ui.Separator(theme.IconSize);
            if (ui.IconButton("remove", ui.State.Icons.AnalyzerRemove, t.AnalyzerRemoveAnalysis, root is not null) && root is not null)
            {
                Commands.Later(ui.State, root, static (a, r) => a.Analyzer.RemoveRoot(r));
            }

            if (ui.IconButton("clear", ui.State.Icons.AnalyzerRemoveAll, t.AnalyzerRemoveAll, any))
            {
                Commands.Later(ui.State, static a => a.Analyzer.ClearRoots());
            }
        }
    }

    private static void Tree(Ui<UiState> ui)
    {
        var rows = ui.State.Analyzer;
        var box = new Box { Padding = new Spacing(0f, ui.Theme.GapTight) };
        var result = ui.TreeView(TreeId, rows.Roots, rows, rows.View, box, toggleOnActivate: false);
        var treeId = ui.GetId(TreeId);
        if (ui.HasFocus(treeId))
        {
            HandleKeys(ui, rows);
        }

        if (result.Activated)
        {
            Commands.Later(ui.State, result.Node, static (a, n) => GoTo(a, n));
        }

        if (result.ContextMenuRequested && result.Node is AnalyzerSymbolNode)
        {
            ui.OpenContextMenu(MenuId, result.ContextMenuAt);
        }
    }

    private static void HandleKeys(Ui<UiState> ui, AnalyzerRows rows)
    {
        if (ui.IsPressed(Shortcuts.Analyze))
        {
            AnalyzeSelected(ui.State, rows);
        }

        if (ui.IsPressed(Shortcuts.Delete))
        {
            RemoveSelectedAnalysis(ui.State, rows);
        }
    }

    private static void AnalyzeSelected(UiState screen, AnalyzerRows rows)
    {
        if (rows.View.Selected is AnalyzerSymbolNode picked)
        {
            Commands.Later(screen, (rows, symbol: picked.Symbol), static (a, s) => Analyze(s.rows, a, s.symbol));
        }
    }

    private static void RemoveSelectedAnalysis(UiState screen, AnalyzerRows rows)
    {
        var root = Analysis.FindSelectedRoot(rows);
        if (root is not null)
        {
            Commands.Later(screen, root, static (a, r) => a.Analyzer.RemoveRoot(r));
        }
    }

    private static void GoTo(AppState appState, AnalyzerNode? node)
    {
        if (node is not AnalyzerSymbolNode symbol)
        {
            return;
        }

        var reference = symbol.Symbol.Reference;
        if (reference == null)
        {
            return;
        }

        Navigate(appState, reference, inNewTab: false);
    }

    private static void ContextMenu(Ui<UiState> ui, AnalyzerTree analyzer)
    {
        var t = ui.State.Strings;
        var rows = ui.State.Analyzer;
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        if (rows.View.Selected is not AnalyzerSymbolNode node)
        {
            return;
        }

        var isRoot = analyzer.HasRoot(node);
        var target = node.Symbol.Reference;
        if (target != null)
        {
            GoToItems(ui, target);
        }

        if (!isRoot && ui.MenuItem(t.TreeAnalyze, Shortcuts.Analyze, id: "analyze"))
        {
            Commands.Later(ui.State, (rows, symbol: node.Symbol), static (a, s) => Analyze(s.rows, a, s.symbol));
        }

        ui.MenuSeparator();
        if (isRoot && ui.MenuItem(t.AnalyzerRemove, id: "remove"))
        {
            Commands.Later(ui.State, node, static (a, n) => a.Analyzer.RemoveRoot(n));
        }

        if (ui.MenuItem(t.AnalyzerClear, id: "clear"))
        {
            Commands.Later(ui.State, static a => a.Analyzer.ClearRoots());
        }
    }

    private static void GoToItems(Ui<UiState> ui, CodeReference.Navigable target)
    {
        var t = ui.State.Strings;
        if (ui.MenuItem(t.AnalyzerGoTo, Shortcuts.Enter, id: "go-to"))
        {
            Commands.Later(ui.State, target, static (a, r) => Navigate(a, r, inNewTab: false));
        }

        if (ui.MenuItem(t.CodeOpenInNewTab, id: "open-in-new-tab"))
        {
            Commands.Later(ui.State, target, static (a, r) => Navigate(a, r, inNewTab: true));
        }
    }

    /// <summary>Analyzes the type or member <paramref name="node"/> stands for in the analyzer, which
    /// is opened if it is not, and selects its root.</summary>
    public static void Analyze(UiState screen, AppState appState, BrowseNode node)
    {
        var root = AddToAnalyzer(appState, node);
        if (root is null)
        {
            return;
        }

        OpenAnalyzer(screen);
        Analysis.ShowRoot(screen.Analyzer, root);
    }

    /// <summary>Analyzes <paramref name="symbol"/>, or finds the root analyzing it already, and shows that root.</summary>
    internal static void Analyze(AnalyzerRows rows, AppState appState, AnalysisSymbol symbol)
    {
        var root = AddToAnalyzer(appState, symbol);
        Analysis.ShowRoot(rows, root);
    }

    /// <summary>Opens the analyzer if it is not, where a root shows as loading while what
    /// <paramref name="reference"/> points at is found and analyzed.</summary>
    public static void Analyze(UiState screen, AppState appState, CodeReference.Navigable reference)
    {
        OpenAnalyzer(screen);
        AddToAnalyzer(appState, reference);
    }
}
