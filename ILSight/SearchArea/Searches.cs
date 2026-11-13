using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>Sorting a search's results into the order asked for (<see cref="SearchResults.StartSort"/>), off the UI
/// thread.</summary>
public static class Searches
{
    /// <summary>Sorts the results into the order <paramref name="sorting"/> asked for, off the UI thread, and shows
    /// them so unless a newer order was asked for meanwhile, keeping the selection in view. Results that arrived or
    /// went while it sorted are sorted again with the rest. <paramref name="ct"/> is the sort's token, linked to what
    /// else may cancel it.</summary>
    public static async Task SortAsync(SearchResults results, SearchSortTicket sorting, CancellationToken ct)
    {
        var version = sorting.RowsVersion;
        var sorted = await SortOffThreadAsync(sorting.Rows, sorting.Order, ct);
        while (version != results.RowsVersion)
        {
            version = results.RowsVersion;
            sorted = await SortOffThreadAsync(results.Rows.ToList(), sorting.Order, ct);
        }

        results.TakeSorted(sorting, sorted);
    }

    private static Task<List<SearchResult>> SortOffThreadAsync(IReadOnlyList<SearchResult> rows, SearchOrder order, CancellationToken ct)
    {
        return Task.Run(() => Sort(rows, order), ct);
    }

    /// <summary><paramref name="rows"/> in <paramref name="order"/>, as a list of their own.</summary>
    private static List<SearchResult> Sort(IReadOnlyList<SearchResult> rows, SearchOrder order)
    {
        var sorted = rows.ToList();
        sorted.Sort(order.Compare);
        return sorted;
    }
}
