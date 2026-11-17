using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.ComparisonArea;
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
    public static void Render(Ui ui, DocumentViews documents, Appearance appearance, ScreenDisplay display, IAppCommandWriter commands, Navigation navigation, AssemblyList assemblies, DocumentTab tab, AssemblyComparison comparison)
    {
        var rows = documents.GetView(tab).Comparison;
        rows.SetStyle(ui.Theme, appearance.NodeIconPalette, appearance.Icons, appearance.Strings);
        rows.ShowDiff(FindDiff(comparison));
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f, Background = ui.Theme.PaneBackground });
        if (!display.Options.ShowOmnibar)
        {
            OwnToolbar(ui, appearance, documents, commands, assemblies, tab, comparison);
        }

        Summary(ui, appearance, comparison);
        var hint = FindHint(ui, appearance, comparison, rows);
        if (hint is not null)
        {
            DocumentsView.Hint(ui, hint);
            return;
        }
        Tree(ui, commands, navigation, rows);
        ContextMenu(ui, appearance, commands, navigation, rows);
    }

    // For when the bar over documents, where the tools sit otherwise, is hidden.
    private static void OwnToolbar(Ui ui, Appearance appearance, DocumentViews documents, IAppCommandWriter commands, AssemblyList assemblies, DocumentTab tab, AssemblyComparison comparison)
    {
        using (ui.PaneToolbar())
        {
            Tools(ui, appearance, documents, commands, assemblies, tab, comparison);
        }

        ui.Separator();
    }

    /// <summary>The comparison's tools, for the bar over the document: collapsing and expanding the tree,
    /// swapping the assemblies' sides, and what it shows; a change compares <paramref name="assemblies"/> as they are
    /// open again, and so is unavailable, saying why, once one of the two was closed.</summary>
    public static void Tools(Ui ui, Appearance appearance, DocumentViews documents, IAppCommandWriter commands, AssemblyList assemblies, DocumentTab tab, AssemblyComparison comparison)
    {
        var t = appearance.Strings;
        var icons = appearance.Icons;
        var rows = documents.GetView(tab).Comparison;
        var any = rows.Roots.Count > 0;
        var why = DocumentsView.FindClosedText(ui, appearance, tab.ClosedAssembly);
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
                var swapped = Comparisons.SwapComparedAssemblies(assemblies, comparison);
                Commands.CompareLater(commands, swapped);
            }

            ui.Separator(ui.Theme.IconSize);
            OnlyDifferencesCheckbox(ui, appearance, rows);
            HiddenMembersCheckbox(ui, appearance, commands, assemblies, comparison, why is null);
        }
    }

    private static void OnlyDifferencesCheckbox(Ui ui, Appearance appearance, ComparisonRows rows)
    {
        var only = rows.OnlyDifferences;
        if (ui.Checkbox(appearance.Strings.ComparisonOnlyDifferences, ref only, id: "only-differences"))
        {
            rows.ShowOnlyDifferences(only);
        }
    }

    private static void HiddenMembersCheckbox(Ui ui, Appearance appearance, IAppCommandWriter commands, AssemblyList assemblies, AssemblyComparison comparison, bool enabled)
    {
        var all = comparison.Scope == ApiScope.AllDeclared;
        if (ui.Checkbox(appearance.Strings.ComparisonShowHiddenMembers, ref all, id: "show-hidden-members", enabled: enabled))
        {
            Commands.CompareLater(commands, Comparisons.ChangeComparedScope(assemblies, comparison, ScopeOf(all)));
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

    private static void Summary(Ui ui, Appearance appearance, AssemblyComparison comparison)
    {
        var diff = FindDiff(comparison);
        if (diff is null)
        {
            return;
        }

        var t = appearance.Strings;
        var theme = ui.Theme;
        using (ui.Row(new Box { Padding = new Spacing(theme.Gap, theme.GapTight), Gap = theme.Gap }))
        {
            ui.Text(OneLineText.Name(diff.LeftName) + Arrow + OneLineText.Name(diff.RightName));
            ui.Text(t.ComparisonAddedCount(diff.Added), color: theme.Success);
            ui.Text(t.ComparisonRemovedCount(diff.Removed), color: theme.Danger);
            ui.Text(t.ComparisonChangedCount(diff.Changed), color: theme.Warning);
            if (comparison.Work.IsRunning)
            {
                ui.Text(t.ComparisonComparing, color: theme.TextMuted);
            }
        }
        ui.Separator();
    }

    private static string? FindHint(Ui ui, Appearance appearance, AssemblyComparison comparison, ComparisonRows rows)
    {
        var t = appearance.Strings;
        return comparison.Result switch
        {
            ComparisonResult.NotYet => t.ComparisonComparing,
            ComparisonResult.Compared => FindNoDifferencesHint(t, rows),
            ComparisonResult.Failed failed => ui.Memo("comparison-failure", (failed.Failure, t), static a => DescribeFailure(a.t, a.Failure)),
        };
    }

    private static ApiDiff? FindDiff(AssemblyComparison comparison)
    {
        return comparison.Result switch
        {
            ComparisonResult.Compared compared => compared.Diff,
            ComparisonResult.NotYet or ComparisonResult.Failed => null,
        };
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
        return failure switch
        {
            ComparisonFailure.AssemblyClosed closed => t.ComparisonAssemblyClosed(closed.File),
            ComparisonFailure.Failed failed => t.ErrorCompare(left: failed.Left, right: failed.Right),
        };
    }

    private static void Tree(Ui ui, IAppCommandWriter commands, Navigation navigation, ComparisonRows rows)
    {
        var box = new Box { Padding = new Spacing(0f, ui.Theme.GapTight) };
        var result = ui.TreeView(TreeId, rows.Roots, rows, rows.View, box);
        if (result.Activated)
        {
            Open(commands, navigation, result.Node, inNewTab: false);
        }

        if (result.ContextMenuRequested && result.Node is not null)
        {
            ui.OpenContextMenu(MenuId, result.ContextMenuAt);
        }
    }

    private static void Open(IAppCommandWriter commands, Navigation navigation, ApiEntry? entry, bool inNewTab)
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

        var resolveTicket = navigation.BeginResolving(inNewTab);
        Commands.GoToReferenceLater(commands, side.Reference, resolveTicket);
    }

    private static ApiSide? LeftOf(ApiEntry entry)
    {
        if (entry.Kind is not ApiEntryKind.Entity entity)
        {
            return null;
        }

        return entity.Sides switch
        {
            ApiSides.LeftOnly sides => sides.Left,
            ApiSides.RightOnly => null,
            ApiSides.Both sides => sides.Left,
        };
    }

    private static ApiSide? RightOf(ApiEntry entry)
    {
        if (entry.Kind is not ApiEntryKind.Entity entity)
        {
            return null;
        }

        return entity.Sides switch
        {
            ApiSides.LeftOnly => null,
            ApiSides.RightOnly sides => sides.Right,
            ApiSides.Both sides => sides.Right,
        };
    }

    private static void ContextMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, Navigation navigation, ComparisonRows rows)
    {
        var t = appearance.Strings;
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
            var resolveTicket = navigation.BeginResolving(inNewTab: false);
            Commands.GoToReferenceLater(commands, left.Reference, resolveTicket);
        }

        if (ui.MenuItem(t.ComparisonOpenRight, enabled: right != null, id: "open-right") && right != null)
        {
            var resolveTicket = navigation.BeginResolving(inNewTab: false);
            Commands.GoToReferenceLater(commands, right.Reference, resolveTicket);
        }
    }
}
