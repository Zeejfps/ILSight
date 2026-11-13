using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.AssemblyArea;

namespace ILSight;

/// <summary>A comparison's tab: which assemblies are compared and how much changed, over the tree of what
/// changed; its tools sit in the bar over the document. Activating an entry opens its code in the right
/// assembly, or in the left when the right lacks it; its menu opens either.</summary>
internal static class ComparisonView
{
    private const string TreeId = "comparison";
    private const string MenuId = "comparison-menu";
    private const string Arrow = "  →  ";

    /// <summary>The comparison <paramref name="tab"/> shows. Its tools change the comparison in this frame and compare
    /// <paramref name="assemblies"/> as they are open, queuing only the comparison's run; opening an entry's code
    /// begins following it in <paramref name="navigation"/> in this frame, queuing only its lookup.</summary>
    public static void Render(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies, DocumentTab tab, AssemblyComparison comparison)
    {
        var rows = DocumentPanels.GetView(ui.State.Documents, tab).Comparison;
        rows.SetStyle(ui.Theme, ui.State.NodeIconPalette, ui.State.Icons, ui.State.Strings);
        rows.FollowDiff(FindDiff(comparison));
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f, Background = ui.Theme.PaneBackground });
        if (!ui.State.Display.ShowOmnibar)
        {
            OwnToolbar(ui, assemblies, tab, comparison);
        }

        Summary(ui, comparison);
        var hint = FindHint(ui, comparison, rows);
        if (hint is not null)
        {
            DocumentsView.Hint(ui, hint);
            return;
        }
        Tree(ui, navigation, rows);
        ContextMenu(ui, navigation, rows);
    }

    // For when the bar over documents, where the tools sit otherwise, is hidden.
    private static void OwnToolbar(Ui<UiState> ui, AssemblyList assemblies, DocumentTab tab, AssemblyComparison comparison)
    {
        using (ui.PaneToolbar())
        {
            Tools(ui, assemblies, tab, comparison);
        }

        ui.Separator();
    }

    /// <summary>The comparison's tools, for the bar over the document: collapsing and expanding the tree,
    /// swapping the assemblies' sides, and what it shows; a change compares <paramref name="assemblies"/> as they are
    /// open again, and so is unavailable, saying why, once one of the two was closed.</summary>
    public static void Tools(Ui<UiState> ui, AssemblyList assemblies, DocumentTab tab, AssemblyComparison comparison)
    {
        var t = ui.State.Strings;
        var icons = ui.State.Icons;
        var rows = DocumentPanels.GetView(ui.State.Documents, tab).Comparison;
        var any = rows.Roots.Count > 0;
        var why = DocumentsView.FindClosedText(ui, tab.ClosedAssembly);
        using (ui.Row(new Box { Gap = ui.Theme.Gap, AlignY = Align.Center }))
        {
            if (ui.IconButton("collapse", icons.AnalyzerCollapseAll, t.CodeCollapseAll, any))
            {
                rows.CollapseAll();
            }

            if (ui.IconButton("expand", icons.AnalyzerExpandAll, t.CodeExpandAll, any))
            {
                rows.ExpandAll();
            }

            ui.Separator(ui.Theme.IconSize);
            if (ui.IconButton("swap", icons.ComparisonSwap, why ?? t.ComparisonSwap, enabled: why is null))
            {
                Commands.CompareLater(ui.State, Comparisons.SwapComparedAssemblies(assemblies, comparison));
            }

            ui.Separator(ui.Theme.IconSize);
            OnlyDifferencesCheckbox(ui, rows);
            HiddenMembersCheckbox(ui, assemblies, comparison, why is null);
        }
    }

    private static void OnlyDifferencesCheckbox(Ui<UiState> ui, ComparisonRows rows)
    {
        var only = rows.OnlyDifferences;
        if (ui.Checkbox(ui.State.Strings.ComparisonOnlyDifferences, ref only, id: "only-differences"))
        {
            rows.ShowOnlyDifferences(only);
        }
    }

    private static void HiddenMembersCheckbox(Ui<UiState> ui, AssemblyList assemblies, AssemblyComparison comparison, bool enabled)
    {
        var all = comparison.Scope == ApiScope.AllDeclared;
        if (ui.Checkbox(ui.State.Strings.ComparisonShowHiddenMembers, ref all, id: "show-hidden-members", enabled: enabled))
        {
            Commands.CompareLater(ui.State, Comparisons.ChangeComparedScope(assemblies, comparison, ScopeOf(all)));
        }
    }

    private static ApiScope ScopeOf(bool all)
    {
        if (all)
        {
            return ApiScope.AllDeclared;
        }

        return ApiScope.PublicApi;
    }

    private static void Summary(Ui<UiState> ui, AssemblyComparison comparison)
    {
        var diff = FindDiff(comparison);
        if (diff is null)
        {
            return;
        }

        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.Row(new Box { Padding = new Spacing(theme.Gap, theme.GapTight), Gap = theme.Gap }))
        {
            ui.Text(diff.LeftName + Arrow + diff.RightName);
            ui.Text(t.ComparisonAddedCount(diff.Added), color: theme.Success);
            ui.Text(t.ComparisonRemovedCount(diff.Removed), color: theme.Danger);
            ui.Text(t.ComparisonChangedCount(diff.Changed), color: theme.Warning);
            if (comparison.IsComparing)
            {
                ui.Text(t.ComparisonComparing, color: theme.TextMuted);
            }
        }
        ui.Separator();
    }

    private static string? FindHint(Ui<UiState> ui, AssemblyComparison comparison, ComparisonRows rows)
    {
        return comparison.Result.Match<(Ui<UiState> ui, ComparisonRows rows), string?>((ui, rows),
            static (s, _) => s.ui.State.Strings.ComparisonComparing,
            static (s, _) => FindNoDifferencesHint(s.ui.State.Strings, s.rows),
            static (s, failed) => s.ui.Memo("comparison-failure", (failed.Failure, t: s.ui.State.Strings), static a => DescribeFailure(a.t, a.Failure)));
    }

    private static ApiDiff? FindDiff(AssemblyComparison comparison)
    {
        return comparison.Result.Match<ApiDiff?>(static _ => null, static compared => compared.Diff, static _ => null);
    }

    private static string? FindNoDifferencesHint(Strings t, ComparisonRows rows)
    {
        if (rows.Roots.Count == 0)
        {
            return t.ComparisonNoDifferences;
        }

        return null;
    }

    private static string DescribeFailure(Strings t, ComparisonFailure failure)
    {
        return failure.Match(t,
            static (t, closed) => t.ComparisonAssemblyClosed(closed.File),
            static (t, failed) => t.ErrorCompare(failed.Left, failed.Right));
    }

    private static void Tree(Ui<UiState> ui, Navigation navigation, ComparisonRows rows)
    {
        var box = new Box { Padding = new Spacing(0f, ui.Theme.GapTight) };
        var result = ui.TreeView(TreeId, rows.Roots, rows, rows.View, box);
        if (result.Activated)
        {
            Open(ui.State, navigation, result.Node, inNewTab: false);
        }

        if (result.ContextMenuRequested && result.Node is not null)
        {
            ui.OpenContextMenu(MenuId, result.ContextMenuAt);
        }
    }

    private static void Open(UiState screen, Navigation navigation, ApiEntry? entry, bool inNewTab)
    {
        if (entry is null)
        {
            return;
        }

        var side = RightOf(entry) ?? LeftOf(entry);
        if (side == null)
        {
            return;
        }

        Commands.FollowLater(screen, side.Reference, navigation.BeginResolving(inNewTab));
    }

    private static ApiSide? LeftOf(ApiEntry entry)
    {
        if (entry is not ApiEntry.Entity entity)
        {
            return null;
        }

        return entity.Sides.Match<ApiSide?>(
            static sides => sides.Left,
            static _ => null,
            static sides => sides.Left);
    }

    private static ApiSide? RightOf(ApiEntry entry)
    {
        if (entry is not ApiEntry.Entity entity)
        {
            return null;
        }

        return entity.Sides.Match<ApiSide?>(
            static _ => null,
            static sides => sides.Right,
            static sides => sides.Right);
    }

    private static void ContextMenu(Ui<UiState> ui, Navigation navigation, ComparisonRows rows)
    {
        var t = ui.State.Strings;
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var entry = rows.View.Selected;
        if (entry is null)
        {
            return;
        }

        var left = LeftOf(entry);
        var right = RightOf(entry);
        if (ui.MenuItem(t.ComparisonOpenLeft, enabled: left != null, id: "open-left") && left != null)
        {
            Commands.FollowLater(ui.State, left.Reference, navigation.BeginResolving(inNewTab: false));
        }

        if (ui.MenuItem(t.ComparisonOpenRight, enabled: right != null, id: "open-right") && right != null)
        {
            Commands.FollowLater(ui.State, right.Reference, navigation.BeginResolving(inNewTab: false));
        }
    }
}
