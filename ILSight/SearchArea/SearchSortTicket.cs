using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>A sort <see cref="Searches.StartSort"/> started, for <see cref="Searches.SortAsync"/> to run: the order
/// asked for, the results as they were when it was asked for, and the work it runs as, which a newer sort or a new
/// search cancels.</summary>
[MustUse]
public sealed class SearchSortTicket
{
    public required SearchOrder Order { get; init; }

    public required IReadOnlyList<SearchResult> Rows { get; init; }

    /// <summary>The <see cref="SearchSorting.RowsVersion"/> <see cref="Rows"/> are of.</summary>
    public required int RowsVersion { get; init; }

    public required WorkTicket Work { get; init; }
}
