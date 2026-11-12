namespace ILSight.SearchArea;

/// <summary>An order of the search results: what they are sorted by, and whether that runs backwards.</summary>
public sealed record SearchOrder
{
    public static SearchOrder ByRelevance { get; } = new() { Sort = SearchSort.Relevance, Descending = false };

    public required SearchSort Sort { get; init; }

    public required bool Descending { get; init; }
}
