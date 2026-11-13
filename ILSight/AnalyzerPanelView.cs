using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Analysis;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;

namespace ILSight;

internal static class AnalyzerPanelView
{
    private const string TreeId = "analyzer";
    private const string MenuId = "analyzer-menu";

    public static void Render(Ui<UiState> ui, Navigation navigation, AnalyzerTree analyzer)
    {
        var t = ui.State.Strings;
        var rows = ui.State.Analyzer;
        rows.SetStyle(ui.Theme, ui.State.NodeIconPalette, ui.State.Icons, t);
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        Toolbar(ui, analyzer, rows);
        ui.Separator();
        if (rows.Roots.Count == 0)
        {
            DocumentsView.Hint(ui, t.AnalyzerHint(Shortcuts.Analyze.Label));
            return;
        }
        Tree(ui, navigation, analyzer);
        ContextMenu(ui, navigation, analyzer);
    }

    private static void Toolbar(Ui<UiState> ui, AnalyzerTree analyzer, AnalyzerRows rows)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var any = analyzer.Roots.Count > 0;
        var root = rows.FindSelectedRoot();
        using (ui.PaneToolbar())
        {
            if (ui.IconButton("collapse", ui.State.Icons.AnalyzerCollapseAll, t.CodeCollapseAll, any))
            {
                rows.CollapseAll();
            }

            if (ui.IconButton("expand", ui.State.Icons.AnalyzerExpandAll, t.CodeExpandAll, any))
            {
                rows.ExpandAll();
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

    private static void Tree(Ui<UiState> ui, Navigation navigation, AnalyzerTree analyzer)
    {
        var rows = ui.State.Analyzer;
        var box = new Box { Padding = new Spacing(0f, ui.Theme.GapTight) };
        var result = ui.TreeView(TreeId, rows.Roots, rows, rows.View, box, toggleOnActivate: false);
        var treeId = ui.GetId(TreeId);
        if (ui.HasFocus(treeId))
        {
            HandleKeys(ui, rows, analyzer);
        }

        if (result.Activated)
        {
            GoTo(ui.State, navigation, result.Node);
        }

        if (result.ContextMenuRequested && result.Node is AnalyzerSymbolNode)
        {
            ui.OpenContextMenu(MenuId, result.ContextMenuAt);
        }
    }

    private static void HandleKeys(Ui<UiState> ui, AnalyzerRows rows, AnalyzerTree analyzer)
    {
        if (ui.IsPressed(Shortcuts.Analyze))
        {
            AnalyzeSelected(ui.State, OfferAnalysis(rows, analyzer));
        }

        if (ui.IsPressed(Shortcuts.Delete))
        {
            RemoveSelectedAnalysis(ui.State, rows);
        }
    }

    /// <summary>What Analyze applies to in the analyzer, its menu item and its key alike: a result selected, unless it is
    /// a root, which is analyzed already (not offered); unavailable when its assembly can't be listed.</summary>
    private static CommandOffer<AnalysisSymbol> OfferAnalysis(AnalyzerRows rows, AnalyzerTree analyzer)
    {
        if (rows.View.Selected is not AnalyzerSymbolNode picked || analyzer.HasRoot(picked))
        {
            return new CommandOffer<AnalysisSymbol>.NotOffered();
        }

        return Analysis.OfferAnalysis(analyzer, picked.Symbol);
    }

    /// <summary>Analyzes what <paramref name="offer"/> applies to as the key asks, or reports why it is unavailable
    /// where the user acted; nothing where it isn't offered.</summary>
    private static void AnalyzeSelected(UiState screen, CommandOffer<AnalysisSymbol> offer)
    {
        offer.Switch(
            runs => Commands.AnalyzeLater(screen, runs.Target),
            unavailable => Commands.RefuseLater(screen, unavailable.Why),
            static _ => { });
    }

    private static void RemoveSelectedAnalysis(UiState screen, AnalyzerRows rows)
    {
        var root = rows.FindSelectedRoot();
        if (root is not null)
        {
            Commands.Later(screen, root, static (a, r) => a.Analyzer.RemoveRoot(r));
        }
    }

    /// <summary>Begins following what <paramref name="node"/> stands for, when it stands for a symbol with a
    /// reference, and asks for it to be looked up and shown.</summary>
    private static void GoTo(UiState screen, Navigation navigation, AnalyzerNode? node)
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

        Commands.FollowLater(screen, reference, navigation.BeginResolving(inNewTab: false));
    }

    private static void ContextMenu(Ui<UiState> ui, Navigation navigation, AnalyzerTree analyzer)
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
            GoToItems(ui, navigation, target);
        }

        AnalyzeItem(ui, OfferAnalysis(rows, analyzer));
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

    /// <summary>The menu's Analyze, as <paramref name="offer"/> decides it: offered, it analyzes; unavailable, it shows
    /// disabled, saying why; not offered, it isn't listed.</summary>
    private static void AnalyzeItem(Ui<UiState> ui, CommandOffer<AnalysisSymbol> offer)
    {
        var t = ui.State.Strings;
        offer.Switch(
            runs =>
            {
                if (ui.MenuItem(t.TreeAnalyze, Shortcuts.Analyze, id: "analyze"))
                {
                    Commands.AnalyzeLater(ui.State, runs.Target);
                }
            },
            unavailable => ui.MenuItem(t.TreeAnalyze, Shortcuts.Analyze, enabled: false, id: "analyze",
                detail: unavailable.Why.Words(t).Why),
            static _ => { });
    }

    private static void GoToItems(Ui<UiState> ui, Navigation navigation, CodeReference.Navigable target)
    {
        var t = ui.State.Strings;
        if (ui.MenuItem(t.AnalyzerGoTo, Shortcuts.Enter, id: "go-to"))
        {
            Commands.FollowLater(ui.State, target, navigation.BeginResolving(inNewTab: false));
        }

        if (ui.MenuItem(t.CodeOpenInNewTab, id: "open-in-new-tab"))
        {
            Commands.FollowLater(ui.State, target, navigation.BeginResolving(inNewTab: true));
        }
    }

    /// <summary>Opens the analyzer if it is not and shows <paramref name="root"/>, expanded and selected.</summary>
    public static void ShowAnalysis(UiState screen, AnalyzerSymbolNode root)
    {
        ScreenLayout.OpenPanel(screen, DockView.Analyzer);
        screen.Analyzer.ShowRoot(root);
    }

    /// <summary>Opens the analyzer if it is not, where the root of <paramref name="pending"/> shows as loading while
    /// the assembly of the symbol it analyzes is listed. Returns it, for the edge to list and analyze
    /// (<see cref="AnalyzeReferenceHandler"/>).</summary>
    public static PendingAnalysis ShowListing(UiState screen, PendingAnalysis pending)
    {
        ScreenLayout.OpenPanel(screen, DockView.Analyzer);
        return pending;
    }

    /// <summary>Opens the analyzer if it is not, where a root shows as loading while what
    /// <paramref name="reference"/> points at is found and analyzed. Returns the root with the reference, for the
    /// edge to find and analyze (<see cref="AnalyzeReferenceHandler"/>).</summary>
    public static PendingAnalysis Analyze(UiState screen, AnalyzerTree analyzer, CodeReference.Navigable reference)
    {
        ScreenLayout.OpenPanel(screen, DockView.Analyzer);
        return Analysis.BeginAnalyzing(analyzer, reference);
    }
}
