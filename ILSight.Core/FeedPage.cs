namespace ILSight.Core;

/// <summary>A page of a feed's search: the packages on it, the closest first, and how many match in all.</summary>
public sealed record FeedPage
{
    public required IReadOnlyList<FeedPackage> Packages { get; init; }

    public required int TotalHits { get; init; }

    /// <summary>Where each part of what the feed sent for the page that couldn't be read was (<c>data[3]</c>,
    /// <c>data[5].versions[1]</c>, or <c>resources[2]</c> of its service index), left out of the page.</summary>
    public required IReadOnlyList<string> LeftOut { get; init; }
}
