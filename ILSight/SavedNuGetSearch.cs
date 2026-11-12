namespace ILSight;

/// <summary>What the settings keep of the "open from NuGet" dialog: the feed last searched and
/// whether prereleases counted.</summary>
public sealed record SavedNuGetSearch
{
    public required string FeedUrl { get; init; }

    public required bool IncludePrerelease { get; init; }
}
