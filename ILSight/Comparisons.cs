using ILSight.Localization;
using ZGF.Gui.Immediate.Widgets;
using ZGF.Gui.Immediate;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Comparison;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>Comparisons of two assemblies' APIs, each in a tab of its own: comparing them, swapping their
/// sides, and taking in more or less of them. Each comparison runs in the background against the
/// assemblies open then, and is dropped when another starts or its tab closes meanwhile.</summary>
public static class Comparisons
{
    /// <summary>Compares <paramref name="left"/> with <paramref name="right"/>, in the active tab while it
    /// shows nothing, else in a new tab, titled <paramref name="title"/>.</summary>
    public static void CompareAssemblies(AppState appState, LoadedModule left, LoadedModule right, string title)
    {
        LeaveActiveTab(appState);
        var tab = GetOrOpenEmptyTab(appState);
        var comparison = new AssemblyComparison { LeftPath = left.FilePath, RightPath = right.FilePath };
        Tabs.ShowComparison(tab, title, comparison);
        Tabs.Keep(appState.Tabs, tab);
        Tabs.MakeActive(appState.Tabs, tab);
        Compare(appState, comparison);
    }

    /// <summary>Shows the comparison <paramref name="saved"/> kept in the tab <paramref name="reopened"/> waits in,
    /// and compares its assemblies again, now that the open list's assemblies are listed.</summary>
    internal static void ReopenComparison(AppState appState, ReopenedTab reopened, ComparisonDocumentRecord saved)
    {
        if (!SavedDocuments.FinishWaiting(appState.Reopened, reopened))
        {
            return;
        }

        var comparison = new AssemblyComparison { LeftPath = saved.LeftPath, RightPath = saved.RightPath };
        Comparisons.TakeInScope(comparison, saved.Scope);
        var title = saved.Title ?? string.Empty;
        Tabs.ShowComparison(reopened.Tab, title, comparison);
        Compare(appState, comparison);
    }

    /// <summary>Compares the assemblies of <paramref name="comparison"/> the other way round, the right one on the left.</summary>
    public static void SwapComparedAssemblies(AppState appState, AssemblyComparison comparison)
    {
        Comparisons.SwapSides(comparison);
        Compare(appState, comparison);
    }

    /// <summary>Compares the assemblies of <paramref name="comparison"/> again, taking in what <paramref name="scope"/> says.</summary>
    public static void ChangeComparedScope(AppState appState, AssemblyComparison comparison, ApiScope scope)
    {
        Comparisons.TakeInScope(comparison, scope);
        Compare(appState, comparison);
    }

    /// <summary>Compares the assemblies of <paramref name="comparison"/>, as they are open now, in the
    /// background, and shows what changed unless another comparison started or it was closed meanwhile.
    /// Shows why when either is no longer open or the comparison fails.</summary>
    private static void Compare(AppState appState, AssemblyComparison comparison)
    {
        Background.Start(appState.Work, nameof(CompareAsync), CompareAsync(appState, comparison), problem => Errors.Report(appState, problem));
    }

    private static async Task CompareAsync(AppState appState, AssemblyComparison comparison)
    {
        var left = FindOpen(appState, comparison.LeftPath);
        if (left is null)
        {
            ShowClosed(appState, comparison, comparison.LeftPath);
            return;
        }
        var right = FindOpen(appState, comparison.RightPath);
        if (right is null)
        {
            ShowClosed(appState, comparison, comparison.RightPath);
            return;
        }
        var scope = comparison.Scope;
        var ticket = Comparisons.StartComparing(comparison);
        var compared = await Background.Run(() => ApiEntries.Compare(left, right, scope, ticket.Token));
        if (!Comparisons.FinishComparing(comparison, ticket))
        {
            return;
        }

        switch (compared)
        {
            case Outcome<ApiDiff>.Ok ok:
                Comparisons.ShowDiff(comparison, ok.Value);
                break;
            case Outcome<ApiDiff>.Failed failed:
                ReportFailure(appState, comparison, left, right, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>The open assembly read from <paramref name="filePath"/> whose API can be compared; null when there is none.</summary>
    private static LoadedModule? FindOpen(AppState appState, AssemblyPath filePath)
    {
        foreach (var assembly in appState.Assemblies.Assemblies)
        {
            if (assembly.FilePath.Equals(filePath))
            {
                return assembly;
            }
        }
        return null;
    }

    private static void ShowClosed(AppState appState, AssemblyComparison comparison, AssemblyPath filePath)
    {
        Comparisons.ShowFailure(comparison, new ComparisonFailure.AssemblyClosed(filePath.FileName));
    }

    private static void ReportFailure(AppState appState, AssemblyComparison comparison, LoadedModule left, LoadedModule right, Exception error)
    {
        Comparisons.ShowFailure(comparison, new ComparisonFailure.Failed(left.NameAndVersion, right.NameAndVersion));
        Errors.Report(appState, new AppProblem.ComparisonFailed(left.NameAndVersion, right.NameAndVersion, error));
    }

    /// <summary>Puts the right assembly on the left and the left one on the right.</summary>
    public static void SwapSides(AssemblyComparison comparison)
    {
        (comparison.LeftPath, comparison.RightPath) = (comparison.RightPath, comparison.LeftPath);
        comparison.Version++;
    }

    /// <summary>Takes in what <paramref name="scope"/> says from now on.</summary>
    public static void TakeInScope(AssemblyComparison comparison, ApiScope scope)
    {
        comparison.Scope = scope;
        comparison.Version++;
    }

    /// <summary>Starts a comparison, cancelling the one in flight, which is then dropped, until
    /// <see cref="FinishComparing"/>.</summary>
    public static LoadTicket StartComparing(AssemblyComparison comparison)
    {
        comparison.Work?.Cancel();
        var work = CancellationTokenSource.CreateLinkedTokenSource(comparison.Open.Token);
        comparison.Work = work;
        return new LoadTicket { Work = work };
    }

    /// <summary>Ends the comparison <paramref name="ticket"/> was given for. False when another started since,
    /// or the comparison was closed, and what it brought should be dropped.</summary>
    public static bool FinishComparing(AssemblyComparison comparison, LoadTicket ticket)
    {
        ticket.Work.Dispose();
        if (comparison.Work != ticket.Work)
        {
            return false;
        }

        comparison.Work = null;
        return !comparison.IsClosed;
    }

    public static void ShowDiff(AssemblyComparison comparison, ApiDiff diff)
    {
        comparison.Diff = diff;
        comparison.Failure = null;
    }

    /// <summary>Shows why the assemblies cannot be compared, cancelling the comparison in flight, if any.</summary>
    public static void ShowFailure(AssemblyComparison comparison, ComparisonFailure failure)
    {
        comparison.Work?.Cancel();
        comparison.Work = null;
        comparison.Diff = null;
        comparison.Failure = failure;
    }

    /// <summary>Closes the comparison for good, cancelling what is in flight.</summary>
    public static void CloseComparison(AssemblyComparison comparison)
    {
        comparison.Open.Cancel();
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
        if (sides.Left.Declaration == sides.Right.Declaration)
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
