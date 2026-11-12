using ZGF.Gui.Immediate.Widgets;
using ZGF.Gui.Immediate;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.AssemblyHandling;

namespace ILSight;

/// <summary>Comparisons of two assemblies' APIs, each in a tab of its own: comparing them, swapping their
/// sides, and taking in more or less of them, and the rows a comparison's tab shows. Each comparison is started here,
/// against the assemblies open then, and returns its <see cref="ComparisonRun"/> for the edge to run
/// (<see cref="CompareAssembliesHandler"/>), whose result is dropped when another starts or its tab closes
/// meanwhile.</summary>
public static class Comparisons
{
    /// <summary>Compares <paramref name="left"/> with <paramref name="right"/>, among <paramref name="assemblies"/>,
    /// in <paramref name="navigation"/>'s active tab while it shows nothing, else in a new tab, titled
    /// <paramref name="title"/>, kept open and made active. Returns the comparison it started, for the edge to run;
    /// null when it shows why it can't run.</summary>
    public static ComparisonRun? CompareAssemblies(Navigation navigation, AssemblyList assemblies, LoadedModule left, LoadedModule right, string title)
    {
        navigation.LeaveActiveTab();
        var tab = navigation.GetOrOpenEmptyTab();
        var comparison = new AssemblyComparison { LeftPath = left.FilePath, RightPath = right.FilePath };
        Tabs.ShowComparison(tab, title, comparison);
        Tabs.Keep(navigation.Tabs, tab);
        Tabs.MakeActive(navigation.Tabs, tab);
        return StartComparing(assemblies, comparison);
    }

    /// <summary>Shows the comparison <paramref name="saved"/> kept in the tab <paramref name="reopened"/> waits in,
    /// one of <paramref name="tabs"/>, and compares its assemblies again among <paramref name="assemblies"/>, now that
    /// the open list's assemblies are listed. Returns the comparison it started, for the edge to run; null when the tab
    /// waits no more, as when it was pointed elsewhere, or when it shows why the comparison can't run.</summary>
    internal static ComparisonRun? ReopenComparison(ReopenedTabs tabs, AssemblyList assemblies, ReopenedTab reopened, ComparisonDocumentRecord saved)
    {
        if (!SavedDocuments.TryFinishWaiting(tabs, reopened))
        {
            return null;
        }

        var comparison = new AssemblyComparison { LeftPath = saved.LeftPath, RightPath = saved.RightPath };
        comparison.TakeInScope(saved.Scope);
        var title = saved.Title ?? string.Empty;
        Tabs.ShowComparison(reopened.Tab, title, comparison);
        return StartComparing(assemblies, comparison);
    }

    /// <summary>Compares the assemblies of <paramref name="comparison"/> the other way round, the right one on the
    /// left, among <paramref name="assemblies"/>. Returns the comparison it started, as
    /// <see cref="CompareAssemblies"/> does.</summary>
    public static ComparisonRun? SwapComparedAssemblies(AssemblyList assemblies, AssemblyComparison comparison)
    {
        comparison.SwapSides();
        return StartComparing(assemblies, comparison);
    }

    /// <summary>Compares the assemblies of <paramref name="comparison"/> again among <paramref name="assemblies"/>,
    /// taking in what <paramref name="scope"/> says. Returns the comparison it started, as
    /// <see cref="CompareAssemblies"/> does.</summary>
    public static ComparisonRun? ChangeComparedScope(AssemblyList assemblies, AssemblyComparison comparison, ApiScope scope)
    {
        comparison.TakeInScope(scope);
        return StartComparing(assemblies, comparison);
    }

    /// <summary>Starts comparing the assemblies of <paramref name="comparison"/> as they are open now among
    /// <paramref name="assemblies"/>, cancelling the comparison of it in flight, and returns what
    /// <see cref="CompareAssembliesHandler"/> needs to run it. Shows why, and returns null, when either is no longer
    /// open.</summary>
    private static ComparisonRun? StartComparing(AssemblyList assemblies, AssemblyComparison comparison)
    {
        var left = FindOpen(assemblies, comparison.LeftPath);
        if (left is null)
        {
            ShowClosed(comparison, comparison.LeftPath);
            return null;
        }
        var right = FindOpen(assemblies, comparison.RightPath);
        if (right is null)
        {
            ShowClosed(comparison, comparison.RightPath);
            return null;
        }

        var work = comparison.Work.Start();
        return new ComparisonRun { Comparison = comparison, Left = left, Right = right, Scope = comparison.Scope, Work = work };
    }

    /// <summary>The open assembly read from <paramref name="filePath"/> whose API can be compared; null when there is none.</summary>
    private static LoadedModule? FindOpen(AssemblyList assemblies, AssemblyPath filePath)
    {
        foreach (var assembly in assemblies.Assemblies)
        {
            if (assembly.FilePath.Equals(filePath))
            {
                return assembly;
            }
        }
        return null;
    }

    private static void ShowClosed(AssemblyComparison comparison, AssemblyPath filePath)
    {
        comparison.ShowFailure(new ComparisonFailure.AssemblyClosed(filePath.FileName));
    }

    /// <summary>Draws the rows in <paramref name="theme"/>, <paramref name="icons"/> and <paramref name="strings"/>,
    /// building them again when any of them changed.</summary>
    public static void SetRowStyle(ComparisonRows rows, UiTheme theme, NodeIconPalette iconColors, AppIcons icons, Strings strings)
    {
        if (IsStyledAs(rows, theme, iconColors, icons, strings))
        {
            return;
        }

        rows.Theme = theme;
        rows.IconColors = iconColors;
        rows.Icons = icons;
        rows.Strings = strings;
        rows.Rows.Clear();
    }

    private static bool IsStyledAs(ComparisonRows rows, UiTheme theme, NodeIconPalette iconColors, AppIcons icons, Strings strings)
    {
        var isStyled = theme == rows.Theme && iconColors == rows.IconColors;
        if (!isStyled)
        {
            return false;
        }

        return icons == rows.Icons && strings == rows.Strings;
    }

    /// <summary>Shows <paramref name="diff"/>, listing the rows anew when it is not the one shown.</summary>
    public static void FollowDiff(ComparisonRows rows, ApiDiff? diff)
    {
        if (diff == rows.ShownDiff)
        {
            return;
        }

        rows.ShownDiff = diff;
        rows.Rows.Clear();
        ListRoots(rows);
        ExpandChangedNamespaces(rows);
    }

    /// <summary>Shows only the entries that differ when <paramref name="only"/>, else all of them.</summary>
    public static void ShowOnlyDifferences(ComparisonRows rows, bool only)
    {
        if (only == rows.OnlyDifferences)
        {
            return;
        }

        rows.OnlyDifferences = only;
        rows.Changes++;
        ListRoots(rows);
    }

    public static ComparisonViewRecord SaveRowsView(ComparisonRows rows)
    {
        return new ComparisonViewRecord(rows.OnlyDifferences);
    }

    /// <summary>Shows the rows as <paramref name="saved"/> says they were shown.</summary>
    public static void RestoreRowsView(ComparisonRows rows, ComparisonViewRecord saved)
    {
        ShowOnlyDifferences(rows, saved.OnlyDifferences);
    }

    private static void ListRoots(ComparisonRows rows)
    {
        rows.Roots = ListShown(rows, ListNamespaces(rows));
        rows.View.Refresh();
    }

    private static IReadOnlyList<ApiEntry> ListNamespaces(ComparisonRows rows)
    {
        if (rows.ShownDiff is null)
        {
            return [];
        }

        return rows.ShownDiff.Namespaces;
    }

    private static void ExpandChangedNamespaces(ComparisonRows rows)
    {
        foreach (var root in rows.Roots)
        {
            if (root.HasChanges)
            {
                rows.View.SetExpanded(root, true);
            }
        }
        rows.View.Refresh();
    }

    public static void ExpandAllRows(ComparisonRows rows)
    {
        foreach (var root in rows.Roots)
        {
            ExpandWithChildren(rows, root);
        }

        rows.View.Refresh();
    }

    private static void ExpandWithChildren(ComparisonRows rows, ApiEntry entry)
    {
        if (!HasChildren(rows, entry))
        {
            return;
        }

        rows.View.SetExpanded(entry, true);
        foreach (var child in ListChildren(rows, entry))
        {
            ExpandWithChildren(rows, child);
        }
    }

    public static void CollapseAllRows(ComparisonRows rows)
    {
        rows.View.CollapseAll();
    }

    public static bool HasChildren(ComparisonRows rows, ApiEntry entry)
    {
        if (!rows.OnlyDifferences)
        {
            return entry.Children.Count > 0;
        }

        return HoldsChanges(entry);
    }

    /// <summary>Whether a child of <paramref name="entry"/> differs or holds one that does. Allocates nothing.</summary>
    private static bool HoldsChanges(ApiEntry entry)
    {
        foreach (var child in entry.Children)
        {
            if (child.HasChanges)
            {
                return true;
            }
        }
        return false;
    }

    public static IReadOnlyList<ApiEntry> ListChildren(ComparisonRows rows, ApiEntry entry)
    {
        return ListShown(rows, entry.Children);
    }

    private static IReadOnlyList<ApiEntry> ListShown(ComparisonRows rows, IReadOnlyList<ApiEntry> entries)
    {
        if (!rows.OnlyDifferences)
        {
            return entries;
        }

        return entries
            .Where(static e => e.HasChanges)
            .ToList();
    }

    /// <summary>What tells an entry apart from its siblings, for tests to find its row by: its label, which for a
    /// member is its signature, so overloads differ.</summary>
    public static string GetKey(ApiEntry entry)
    {
        return entry.Text;
    }

    public static TreeRow GetRow(ComparisonRows rows, ApiEntry entry)
    {
        if (rows.Rows.TryGetValue(entry, out var row))
        {
            return row;
        }

        row = CreateRow(rows, entry);
        rows.Rows.Add(entry, row);
        return row;
    }

    private static TreeRow CreateRow(ComparisonRows rows, ApiEntry entry)
    {
        var icon = NodeIcons.Icon(NodeIcons.Of(entry.Shape), entry.Accessibility, rows.IconColors, rows.Icons);
        var color = GetColor(rows, entry);
        var tooltip = DescribeDeclarations(rows, entry);
        var change = NameChange(rows, entry.Change);
        return new TreeRow(entry.Text, icon, color, tooltip, change);
    }

    /// <summary>The color of what was added, removed or changed; muted for what is the same throughout,
    /// and the theme's text color for what holds a difference.</summary>
    private static uint GetColor(ComparisonRows rows, ApiEntry entry)
    {
        return entry.Change switch
        {
            ApiChange.Added => rows.Theme.Success,
            ApiChange.Removed => rows.Theme.Danger,
            ApiChange.Changed => rows.Theme.Warning,
            _ => GetUnchangedColor(rows, entry),
        };
    }

    private static uint GetUnchangedColor(ComparisonRows rows, ApiEntry entry)
    {
        if (entry.HasChanges)
        {
            return 0;
        }

        return rows.Theme.TextMuted;
    }

    /// <summary>How the entry is declared: on both sides when it changed, else on the side it is on;
    /// null for a namespace.</summary>
    private static string? DescribeDeclarations(ComparisonRows rows, ApiEntry entry)
    {
        return entry switch
        {
            ApiEntry.Namespace => null,
            ApiEntry.Entity entity => DescribeDeclarations(rows, entity.Sides),
            _ => throw new UnreachableException(),
        };
    }

    private static string DescribeDeclarations(ComparisonRows rows, ApiSides sides)
    {
        return sides switch
        {
            ApiSides.LeftOnly left => left.Left.Declaration,
            ApiSides.RightOnly right => right.Right.Declaration,
            ApiSides.Both both => DescribeDeclarations(rows, both),
            _ => throw new UnreachableException(),
        };
    }

    private static string DescribeDeclarations(ComparisonRows rows, ApiSides.Both sides)
    {
        if (!sides.IsDeclaredDifferently)
        {
            return sides.Right.Declaration;
        }

        var left = rows.Strings.ComparisonLeftDeclaration(sides.Left.Declaration);
        var right = rows.Strings.ComparisonRightDeclaration(sides.Right.Declaration);
        return left + Environment.NewLine + right;
    }

    private static string? NameChange(ComparisonRows rows, ApiChange change)
    {
        return change switch
        {
            ApiChange.Added => rows.Strings.ComparisonAdded,
            ApiChange.Removed => rows.Strings.ComparisonRemoved,
            ApiChange.Changed => rows.Strings.ComparisonChanged,
            _ => null,
        };
    }
}
