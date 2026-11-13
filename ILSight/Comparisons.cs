using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.AssemblyArea;

namespace ILSight;

/// <summary>Comparisons of two assemblies' APIs, each in a tab of its own: comparing them, swapping their
/// sides, and taking in more or less of them (<see cref="ComparisonRows"/> keeps the rows a comparison's tab shows).
/// Each comparison is started here, against the assemblies open then, and returns its <see cref="ComparisonRun"/> for the edge to run
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
        var comparison = new AssemblyComparison { LeftPath = left.FilePath, RightPath = right.FilePath };
        navigation.OpenComparison(comparison, title);
        return StartComparing(assemblies, comparison);
    }

    /// <summary>Shows the comparison <paramref name="saved"/> kept in the tab <paramref name="reopened"/> waits in,
    /// one of <paramref name="tabs"/>, through <paramref name="navigation"/>, and compares its assemblies again among <paramref name="assemblies"/>, now that
    /// the open list's assemblies are listed. Returns the comparison it started, for the edge to run; null when the tab
    /// waits no more, as when it was pointed elsewhere, or when it shows why the comparison can't run.</summary>
    internal static ComparisonRun? ReopenComparison(Navigation navigation, ReopenedTabs tabs, AssemblyList assemblies, ReopenedTab reopened, ComparisonDocumentRecord saved)
    {
        if (!SavedDocuments.TryFinishWaiting(tabs, reopened))
        {
            return null;
        }

        var comparison = new AssemblyComparison { LeftPath = saved.LeftPath, RightPath = saved.RightPath };
        comparison.TakeInScope(saved.Scope);
        var title = saved.Title ?? string.Empty;
        navigation.RestoreComparison(reopened.Tab, comparison, title);
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
    internal static ComparisonRun? StartComparing(AssemblyList assemblies, AssemblyComparison comparison)
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
}
