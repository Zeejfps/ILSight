namespace ILSight;

/// <summary>The step that asked for a place's lookup, said beside its failure where the user acted: context the report
/// carries, never a kind of its own (<see cref="AppProblem.PlaceLookupFailed"/> is one kind wherever it was asked).</summary>
public union PlaceLookupStep(PlaceLookupStep.GoingToBookmark, PlaceLookupStep.GoingThroughHistory,
    PlaceLookupStep.ReopeningTabs, PlaceLookupStep.ReloadingTab)
{
    /// <summary>Going to the bookmark named <see cref="Name"/>.</summary>
    public sealed record GoingToBookmark(string Name);

    /// <summary>Going to an entry of a tab's history, back or forward.</summary>
    public sealed record GoingThroughHistory;

    /// <summary>Reopening the tabs the open list saved.</summary>
    public sealed record ReopeningTabs;

    /// <summary>Showing a tab's place again once its assembly was read again.</summary>
    public sealed record ReloadingTab;
}
