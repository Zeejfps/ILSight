namespace ILSight.Core;

/// <summary>A page of a feed's search: the packages on it, the closest first, and how many match in all.</summary>
public sealed record FeedPage
{
    public required IReadOnlyList<FeedPackage> Packages { get; init; }

    public required int TotalHits { get; init; }
}
