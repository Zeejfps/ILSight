using System.Diagnostics;

namespace ILSight;

/// <summary>The step that asked for a place's lookup, said beside its failure where the user acted: context the report
/// carries, never a kind of its own (<see cref="AppProblem.PlaceLookupFailed"/> is one kind wherever it was asked).</summary>
public abstract record PlaceLookupStep
{
    private PlaceLookupStep()
    {
    }

    /// <summary>Going to the bookmark named <see cref="Name"/>.</summary>
    public sealed record GoingToBookmark(string Name) : PlaceLookupStep;

    /// <summary>Going to an entry of a tab's history, back or forward.</summary>
    public sealed record GoingThroughHistory : PlaceLookupStep;

    /// <summary>Reopening the tabs the open list saved.</summary>
    public sealed record ReopeningTabs : PlaceLookupStep;

    /// <summary>Showing a tab's place again once its assembly was read again.</summary>
    public sealed record ReloadingTab : PlaceLookupStep;

    /// <summary>What the handler of whichever step this is makes of it.</summary>
    public TResult Match<TResult>(Func<GoingToBookmark, TResult> goingToBookmark, Func<GoingThroughHistory, TResult> goingThroughHistory,
        Func<ReopeningTabs, TResult> reopeningTabs, Func<ReloadingTab, TResult> reloadingTab)
    {
        return this switch
        {
            GoingToBookmark s => goingToBookmark(s),
            GoingThroughHistory s => goingThroughHistory(s),
            ReopeningTabs s => reopeningTabs(s),
            ReloadingTab s => reloadingTab(s),
            _ => throw new UnreachableException($"PlaceLookupStep.Match does not handle {GetType().Name}."),
        };
    }
}
