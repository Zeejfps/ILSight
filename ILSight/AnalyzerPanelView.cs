using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.AnalysisArea;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;

namespace ILSight;

internal static class AnalyzerPanelView
{
    private const string TreeId = "analyzer";
    private const string MenuId = "analyzer-menu";

    public static void Render(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, AnalyzerRows analyzerRows, Navigation navigation, AnalyzerTree analyzer)
    {
        var t = appearance.Strings;
        var rows = analyzerRows;
        rows.SetStyle(ui.Theme, appearance.NodeIconPalette, appearance.Icons, t);
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        Toolbar(ui, appearance, commands, analyzer, rows);
        ui.Separator();
        if (rows.Roots.Count == 0)
        {
            DocumentsView.Hint(ui, t.AnalyzerHint(Shortcuts.Analyze.Label));
            return;
        }
        Tree(ui, commands, panels, analyzerRows, navigation, analyzer);
        ContextMenu(ui, appearance, commands, panels, analyzerRows, navigation, analyzer);
    }

    private static void Toolbar(Ui ui, Appearance appearance, IAppCommandWriter commands, AnalyzerTree analyzer, AnalyzerRows rows)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var any = analyzer.Roots.Count > 0;
        var root = rows.FindSelectedRoot();
        using (ui.PaneToolbar())
        {
            if (ui.IconButton("collapse", appearance.Icons.AnalyzerCollapseAll, t.CodeCollapseAll, any))
            {
                rows.CollapseAll();
            }

            if (ui.IconButton("expand", appearance.Icons.AnalyzerExpandAll, t.CodeExpandAll, any))
            {
                rows.ExpandAll();
            }

            ui.Separator(theme.IconSize);
            if (ui.IconButton("remove", appearance.Icons.AnalyzerRemove, t.AnalyzerRemoveAnalysis, root is not null) && root is not null)
            {
                Commands.Later(commands, root, static (a, r) => a.ListSession.Analyzer.RemoveRoot(r));
            }

            if (ui.IconButton("clear", appearance.Icons.AnalyzerRemoveAll, t.AnalyzerRemoveAll, any))
            {
                Commands.Later(commands, static a => a.ListSession.Analyzer.ClearRoots());
            }
        }
    }

    private static void Tree(Ui ui, IAppCommandWriter commands, Panels panels, AnalyzerRows analyzerRows, Navigation navigation, AnalyzerTree analyzer)
    {
        var rows = analyzerRows;
        var box = new Box { Padding = new Spacing(0f, ui.Theme.GapTight) };
        var result = ui.TreeView(TreeId, rows.Roots, rows, rows.View, box, toggleOnActivate: false);
        var treeId = ui.GetId(TreeId);
        if (ui.HasFocus(treeId))
        {
            RunAnalyzerShortcuts(ui, commands, panels, rows, analyzer);
        }

        if (result.Activated)
        {
            GoTo(commands, navigation, result.Node);
        }

        if (result.ContextMenuRequested && result.Node is AnalyzerSymbolNode)
        {
            ui.OpenContextMenu(MenuId, result.ContextMenuAt);
        }
    }

    private static void RunAnalyzerShortcuts(Ui ui, IAppCommandWriter commands, Panels panels, AnalyzerRows rows, AnalyzerTree analyzer)
    {
        if (ui.IsPressed(Shortcuts.Analyze))
        {
            AnalyzeSelected(commands, panels, rows, OfferAnalysis(rows, analyzer));
        }

        if (ui.IsPressed(Shortcuts.Delete))
        {
            RemoveSelectedAnalysis(commands, rows);
        }
    }

    private static CommandOffer<AnalysisSymbol> OfferAnalysis(AnalyzerRows rows, AnalyzerTree analyzer)
    {
        // A root is analyzed already, so it isn't offered.
        if (rows.View.Selected is not AnalyzerSymbolNode picked || analyzer.HasRoot(picked))
        {
            return new CommandOffer<AnalysisSymbol>.NotOffered();
        }

        return Analysis.OfferAnalysis(analyzer, picked.Symbol);
    }

    private static void AnalyzeSelected(IAppCommandWriter commands, Panels panels, AnalyzerRows rows, CommandOffer<AnalysisSymbol> offer)
    {
        offer.Switch(
            runs => Commands.AnalyzeLater(commands, panels, rows, runs.Target),
            unavailable => Commands.RefuseLater(commands, unavailable.Why),
            static _ => { });
    }

    private static void RemoveSelectedAnalysis(IAppCommandWriter commands, AnalyzerRows rows)
    {
        var root = rows.FindSelectedRoot();
        if (root is not null)
        {
            Commands.Later(commands, root, static (a, r) => a.ListSession.Analyzer.RemoveRoot(r));
        }
    }

    private static void GoTo(IAppCommandWriter commands, Navigation navigation, AnalyzerNode? node)
    {
        if (node is not AnalyzerSymbolNode symbol)
        {
            return;
        }

        var reference = symbol.Symbol.Reference;
        if (reference is null)
        {
            return;
        }

        var resolveTicket = navigation.BeginResolving(inNewTab: false);
        Commands.GoToReferenceLater(commands, reference.Value, resolveTicket);
    }

    private static void ContextMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, AnalyzerRows analyzerRows, Navigation navigation, AnalyzerTree analyzer)
    {
        var t = appearance.Strings;
        var rows = analyzerRows;
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
        if (target is not null)
        {
            GoToItems(ui, appearance, commands, navigation, target.Value);
        }

        AnalyzeItem(ui, appearance, commands, panels, analyzerRows, OfferAnalysis(rows, analyzer));
        ui.MenuSeparator();
        if (isRoot && ui.MenuItem(t.AnalyzerRemove, id: "remove"))
        {
            Commands.Later(commands, node, static (a, n) => a.ListSession.Analyzer.RemoveRoot(n));
        }

        if (ui.MenuItem(t.AnalyzerClear, id: "clear"))
        {
            Commands.Later(commands, static a => a.ListSession.Analyzer.ClearRoots());
        }
    }

    private static void AnalyzeItem(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, AnalyzerRows analyzerRows, CommandOffer<AnalysisSymbol> offer)
    {
        var t = appearance.Strings;
        offer.Switch(
            runs =>
            {
                if (ui.MenuItem(t.TreeAnalyze, Shortcuts.Analyze, id: "analyze"))
                {
                    Commands.AnalyzeLater(commands, panels, analyzerRows, runs.Target);
                }
            },
            unavailable => ui.MenuItem(t.TreeAnalyze, Shortcuts.Analyze, enabled: false, id: "analyze",
                detail: unavailable.Why.Words(t).Why),
            static _ => { });
    }

    private static void GoToItems(Ui ui, Appearance appearance, IAppCommandWriter commands, Navigation navigation, CodeReference.Navigable target)
    {
        var t = appearance.Strings;
        if (ui.MenuItem(t.AnalyzerGoTo, Shortcuts.Enter, id: "go-to"))
        {
            var resolveTicket = navigation.BeginResolving(inNewTab: false);
            Commands.GoToReferenceLater(commands, target, resolveTicket);
        }

        if (ui.MenuItem(t.CodeOpenInNewTab, id: "open-in-new-tab"))
        {
            var resolveTicket = navigation.BeginResolving(inNewTab: true);
            Commands.GoToReferenceLater(commands, target, resolveTicket);
        }
    }

    /// <summary>Opens the analyzer if it is not and shows <paramref name="root"/>, expanded and selected.</summary>
    public static void ShowAnalysis(Panels panels, AnalyzerRows rows, AnalyzerSymbolNode root)
    {
        panels.Open(DockView.Analyzer);
        rows.ShowRoot(root);
    }

    /// <summary>Opens the analyzer if it is not, where the root of <paramref name="pending"/> shows as loading while
    /// the assembly of the symbol it analyzes is listed. Returns it, for the edge to list and analyze
    /// (<see cref="AnalyzeReferenceHandler"/>).</summary>
    public static PendingAnalysis ShowListing(Panels panels, PendingAnalysis pending)
    {
        panels.Open(DockView.Analyzer);
        return pending;
    }

    /// <summary>Opens the analyzer if it is not. The root returned shows as loading until the edge finds and analyzes
    /// what <paramref name="reference"/> points at (<see cref="AnalyzeReferenceHandler"/>).</summary>
    public static PendingAnalysis ShowAnalyzing(Panels panels, AnalyzerTree analyzer, CodeReference.Navigable reference)
    {
        panels.Open(DockView.Analyzer);
        return Analysis.BeginAnalyzing(analyzer, reference);
    }
}
