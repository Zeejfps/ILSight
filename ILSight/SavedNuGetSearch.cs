namespace ILSight;

/// <summary>What the settings keep of the "open from NuGet" dialog: the feed last searched and
/// whether prereleases counted.</summary>
public sealed record SavedNuGetSearch(string FeedUrl, bool IncludePrerelease);
