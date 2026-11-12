using ILSight.Core;

namespace ILSight;

/// <summary>
/// The order a search's results are in, and the order asked for. A header click asks for an order, which the
/// header shows at once, and the results are sorted into it off the UI thread: sorting what a search finds can take
/// longer than a frame. Only the newest order asked for is taken. Results that arrive or go meanwhile go in the
/// order the results are in, and count as a change, so that a sort of the results before them is done again.
/// Used on the UI thread only.
/// </summary>
public sealed class SearchSorting
{
    private readonly LatestWork _sorts = new();

    /// <summary>The order asked for last, which the header shows.</summary>
    public SearchOrder Asked { get; private set; } = SearchOrder.ByRelevance;

    /// <summary>The order the results are in, which results that arrive are merged in by.</summary>
    public SearchOrder Applied { get; private set; } = SearchOrder.ByRelevance;

    /// <summary>Counts the changes to the results other than sorting them.</summary>
    public int RowsVersion { get; private set; }

    /// <summary>True from <see cref="Start"/> until the newest sort is taken or dropped.</summary>
    public bool IsSorting => _sorts.IsRunning;

    /// <summary>Asks for <paramref name="order"/> over <paramref name="rows"/>, the results as they are now, in place
    /// of the sort running, which is cancelled and its result dropped.</summary>
    public SearchSortTicket Start(SearchOrder order, IReadOnlyList<SearchResult> rows)
    {
        Asked = order;
        return new SearchSortTicket { Order = order, Rows = rows.ToList(), RowsVersion = RowsVersion, Work = _sorts.Start() };
    }

    /// <summary>Notes that results arrived or went.</summary>
    public void NoteRowsChanged()
    {
        RowsVersion++;
    }

    /// <summary>Notes that the results were emptied for a new search: they are in any order, so in the one asked for,
    /// and the sort running is dropped.</summary>
    public void NoteRowsEmptied()
    {
        _sorts.Cancel();
        RowsVersion++;
        Applied = Asked;
    }

    /// <summary>Ends the sort <paramref name="ticket"/> was given for: the results are in its order from now on.
    /// False when a newer sort started since, or it was cancelled, and its result should be dropped.</summary>
    public bool TryFinish(SearchSortTicket ticket)
    {
        if (!_sorts.TryFinish(ticket.Work))
        {
            return false;
        }

        Applied = ticket.Order;
        return true;
    }
}
