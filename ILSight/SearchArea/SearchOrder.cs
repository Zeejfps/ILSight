using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>An order of the search results: what they are sorted by, and whether that runs backwards.</summary>
public sealed record SearchOrder
{
    public static SearchOrder ByRelevance { get; } = new() { Sort = SearchSort.Relevance, Descending = false };

    public required SearchSort Sort { get; init; }

    public required bool Descending { get; init; }

    /// <summary>Where <paramref name="a"/> goes against <paramref name="b"/> in this order: below 0 before it, above 0
    /// after it, 0 when they tie.</summary>
    public int Compare(SearchResult a, SearchResult b)
    {
        var compared = CompareAscending(Sort, a, b);
        return Descending ? -compared : compared;
    }

    private static int CompareAscending(SearchSort sort, SearchResult a, SearchResult b)
    {
        return sort switch
        {
            SearchSort.Name => CompareByName(a, b),
            SearchSort.Location => CompareByLocation(a, b),
            SearchSort.Assembly => CompareByAssembly(a, b),
            _ => CompareByRelevance(a, b),
        };
    }

    private static int CompareByName(SearchResult a, SearchResult b)
    {
        return Compare(a.Text, b.Text, a.Location, b.Location, GetAssemblyLabel(a), GetAssemblyLabel(b));
    }

    private static int CompareByLocation(SearchResult a, SearchResult b)
    {
        return Compare(a.Location, b.Location, a.Text, b.Text, GetAssemblyLabel(a), GetAssemblyLabel(b));
    }

    private static int CompareByAssembly(SearchResult a, SearchResult b)
    {
        return Compare(GetAssemblyLabel(a), GetAssemblyLabel(b), a.Text, b.Text, a.Location, b.Location);
    }

    private static int CompareByRelevance(SearchResult a, SearchResult b)
    {
        var fit = b.Fitness.CompareTo(a.Fitness);
        if (fit != 0)
        {
            return fit;
        }

        return CompareByName(a, b);
    }

    private static string GetAssemblyLabel(SearchResult row)
    {
        return NodeText.AssemblyLabel(row.Node.Assembly);
    }

    private static int Compare(string a1, string b1, string a2, string b2, string a3, string b3)
    {
        var order = StringComparer.OrdinalIgnoreCase.Compare(a1, b1);
        if (order != 0)
        {
            return order;
        }

        order = StringComparer.OrdinalIgnoreCase.Compare(a2, b2);
        if (order != 0)
        {
            return order;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(a3, b3);
    }
}
