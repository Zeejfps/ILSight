namespace ILSight.Core;

/// <summary>A version a feed lists for a package, how many times it was downloaded, and where the feed
/// describes it in full; null when the search does not say.</summary>
public sealed record FeedVersion
{
    public required PackageVersion Version { get; init; }

    public required long Downloads { get; init; }

    public required WebUrl? DetailsUrl { get; init; }
}
