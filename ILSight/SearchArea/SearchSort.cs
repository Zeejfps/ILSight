namespace ILSight.SearchArea;

/// <summary>What the search results are sorted by; <see cref="Relevance"/> puts the closest matches first.</summary>
public enum SearchSort
{
    Relevance,
    Name,
    Location,
    Assembly,
}
