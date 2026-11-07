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
        var tab = FindOrOpenEmptyTab(appState);
        var comparison = new AssemblyComparison { LeftPath = left.FilePath, RightPath = right.FilePath };
        tab.ShowComparison(title, comparison);
        appState.Tabs.Keep(tab);
        appState.Tabs.MakeActive(tab);
        Compare(appState, comparison);
    }

    /// <summary>Shows the comparison <paramref name="saved"/> kept in the tab <paramref name="reopened"/> waits in,
    /// and compares its assemblies again, now that the open list's assemblies are listed.</summary>
    internal static void ReopenComparison(AppState appState, ReopenedTab reopened, ComparisonDocumentRecord saved)
    {
        if (!SavedDocuments.FinishWaiting(appState.Reopened, reopened))
            return;
        var comparison = new AssemblyComparison { LeftPath = saved.LeftPath, RightPath = saved.RightPath };
        Comparisons.TakeInScope(comparison, saved.Scope);
        var title = saved.Title ?? string.Empty;
        reopened.Tab.ShowComparison(title, comparison);
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
    private static void Compare(AppState appState, AssemblyComparison comparison) =>
        Background.Start(CompareAsync(appState, comparison), appState.ReportError);

    private static async Task CompareAsync(AppState appState, AssemblyComparison comparison)
    {
        var left = FindOpen(appState, comparison.LeftPath);
        if (left == null)
        {
            ShowClosed(appState, comparison, comparison.LeftPath);
            return;
        }
        var right = FindOpen(appState, comparison.RightPath);
        if (right == null)
        {
            ShowClosed(appState, comparison, comparison.RightPath);
            return;
        }
        var scope = comparison.Scope;
        var ticket = Comparisons.StartComparing(comparison);
        var compared = await Background.Run(() => ApiDiff.Compare(left, right, scope, ticket.Token));
        if (!Comparisons.FinishComparing(comparison, ticket))
            return;
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
                return assembly;
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
        appState.ReportError(new AppProblem.ComparisonFailed(left.NameAndVersion, right.NameAndVersion, error));
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
            return false;
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
}
