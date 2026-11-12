namespace ILSight.Core;

/// <summary>A version a feed lists for a package, how many times it was downloaded, and where the feed
/// describes it in full; null when the search does not say.</summary>
public sealed record FeedVersion(PackageVersion Version, long Downloads, WebUrl? DetailsUrl);
