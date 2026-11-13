using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>What a search found, and its runs: started once the query typed settles, taking in what they find,
/// sorted and picked from. Its rows are the hits of the assemblies it searched only, in the order its sorting says;
/// assemblies opened later are searched and their hits added, and hits in closed ones dropped, keeping the selection.
/// Every part changes only through its methods. Used on the UI thread only.</summary>
public sealed class SearchResults
{
    private const double DebounceSeconds = 0.15;

    private readonly List<SearchResult> _rows = new();
    private readonly HashSet<LoadedAssembly> _searchedAssemblies = new(ReferenceEqualityComparer.Instance);
    private readonly List<SearchFind.Unsearched> _unsearched = new();

    /// <summary>The runs that may still deliver hits.</summary>
    private readonly SearchRuns _runs = new();

    private readonly TypedText _typed = new();

    /// <summary>The order the results are in and the one asked for.</summary>
    private readonly SearchSorting _sorting = new();

    /// <summary>The hits so far, in the order the sort says, of the assemblies searched only.</summary>
    public IReadOnlyList<SearchResult> Rows => _rows;

    /// <summary>The assemblies searched the library failed on while they were searched, and why, in the order they came
    /// in.</summary>
    public IReadOnlyList<SearchFind.Unsearched> Unsearched => _unsearched;

    /// <summary>The query last searched for, or null before the first.</summary>
    public SearchedQuery? LastSearched { get; private set; }

    /// <summary>Why <see cref="LastSearched"/> finds nothing, when its <c>/regex/</c> is no valid
    /// regular expression; else null.</summary>
    public SearchMatcher.InvalidRegex? InvalidRegex { get; private set; }

    public ItemSelection<SearchResult> Selection { get; } = new();

    public SearchResult? SelectedRow => Selection.Item;

    /// <summary>Whether results may still arrive, or be sorted into the order asked for.</summary>
    public bool IsBusy => _runs.IsRunning || _sorting.IsSorting;

    /// <summary>The order asked for last, which the header shows.</summary>
    public SearchOrder AskedOrder => _sorting.Asked;

    /// <summary>The order the rows are in, which rows that arrive are merged in by.</summary>
    public SearchOrder AppliedOrder => _sorting.Applied;

    /// <summary>Counts the changes to the rows other than sorting them.</summary>
    public int RowsVersion => _sorting.RowsVersion;

    /// <summary>Whether runs were asked for that have not been started yet.</summary>
    public bool HasAskedRuns => _runs.HasAsked;

    /// <summary>Starts a search for <paramref name="query"/>, looking for <paramref name="kinds"/>, when the query
    /// settled, or searches assemblies opened since. Does nothing unless the results are <paramref name="shown"/>.
    /// Returns the seconds left before a typed query settles, or 0. Runs every frame, so no lambda belongs in it.</summary>
    public double Update(string query, SearchKinds kinds, AssemblyList assemblies, double now, bool shown)
    {
        _typed.Note(query, now);
        if (!shown)
        {
            return 0;
        }

        var searched = LastSearched;
        if (searched is null || IsQueryChanged(kinds, searched))
        {
            return RestartWhenSettled(kinds, assemblies, now, searched);
        }

        if (assemblies.Version != searched.Version)
        {
            Extend(assemblies, searched);
        }

        return 0;
    }

    private bool IsQueryChanged(SearchKinds kinds, SearchedQuery searched)
    {
        return _typed.Text != searched.Query || kinds != searched.Kinds;
    }

    private double RestartWhenSettled(SearchKinds kinds, AssemblyList assemblies, double now, SearchedQuery? searched)
    {
        var remaining = _typed.CalculateSecondsToSettle(now, DebounceSeconds);
        if (remaining > 0 && searched is not null && kinds == searched.Kinds)
        {
            return remaining;
        }

        Restart(kinds, assemblies);
        return 0;
    }

    private void Restart(SearchKinds kinds, AssemblyList assemblies)
    {
        _runs.CancelAll();
        var searched = new SearchedQuery
        {
            Query = _typed.Text,
            Kinds = kinds,
            Version = assemblies.Version,
        };
        LastSearched = searched;
        _rows.Clear();
        _unsearched.Clear();
        _sorting.NoteRowsEmptied();
        ItemSelections.Clear(Selection);
        var listed = assemblies.Assemblies;
        _searchedAssemblies.Clear();
        _searchedAssemblies.UnionWith(listed);
        Ask(searched, listed);
    }

    private void Extend(AssemblyList assemblies, SearchedQuery searched)
    {
        searched = searched with { Version = assemblies.Version };
        LastSearched = searched;
        var listed = assemblies.Assemblies;
        DropClosed(listed);
        var added = MarkSearched(listed);
        if (added.Count > 0)
        {
            Ask(searched, added);
        }
    }

    private void DropClosed(IReadOnlyList<LoadedModule> listed)
    {
        var closed = _searchedAssemblies.RemoveWhere(a => !listed.Contains(a));
        if (closed == 0)
        {
            return;
        }

        _rows.RemoveAll(r => !_searchedAssemblies.Contains(r.Node.Assembly));
        _unsearched.RemoveAll(u => !_searchedAssemblies.Contains(u.Assembly));
        _sorting.NoteRowsChanged();
        ItemSelections.DropIfGone(Selection, _rows);
    }

    private List<LoadedModule> MarkSearched(IReadOnlyList<LoadedModule> listed)
    {
        var added = new List<LoadedModule>();
        foreach (var assembly in listed)
        {
            if (_searchedAssemblies.Add(assembly))
            {
                added.Add(assembly);
            }
        }
        return added;
    }

    /// <summary>Asks for <paramref name="searched"/> to be run over <paramref name="within"/>, unless it can find
    /// nothing; the edge starts it once the frame is drawn (<see cref="RunSearchHandler"/>).</summary>
    private void Ask(SearchedQuery searched, IReadOnlyList<LoadedModule> within)
    {
        var query = SearchQueries.ParseQuery(searched.Query, searched.Kinds);
        InvalidRegex = query.Matcher as SearchMatcher.InvalidRegex;
        if (!query.CanFind)
        {
            return;
        }

        _runs.Ask(new SearchRun { Query = query, Within = [.. within] });
    }

    /// <summary>The runs asked for since the last time, for the edge to start.</summary>
    public List<SearchRun> TakeAskedRuns()
    {
        return _runs.TakeAsked();
    }

    /// <summary>Whether <paramref name="run"/> was not replaced by a new query.</summary>
    public bool IsCurrent(SearchRun run)
    {
        return _runs.IsCurrent(run);
    }

    /// <summary>Ends <paramref name="run"/>, which delivered all it found.</summary>
    public void EndRun(SearchRun run)
    {
        _runs.End(run);
    }

    /// <summary>Merges the hits of <paramref name="batch"/> that are still wanted into the rows, in their order, and
    /// notes the assemblies it couldn't search that are still searched, unless a new query replaced
    /// <paramref name="run"/>, which found them.</summary>
    public void Deliver(IReadOnlyList<SearchFind> batch, SearchRun run)
    {
        if (!_runs.IsCurrent(run))
        {
            return;
        }

        var hits = new List<SearchResult>();
        foreach (var find in batch)
        {
            find.Switch(hit => hits.Add(hit.Result), NoteUnsearched);
        }

        var arrived = SortArrived(hits);
        MergeIntoRows(arrived);
        _sorting.NoteRowsChanged();
    }

    private void NoteUnsearched(SearchFind.Unsearched unsearched)
    {
        if (!_searchedAssemblies.Contains(unsearched.Assembly))
        {
            return;
        }

        _unsearched.Add(unsearched);
    }

    /// <summary>The hits of <paramref name="batch"/> still wanted, in the order the rows are in, which a comparer
    /// is made for per batch.</summary>
    private List<SearchResult> SortArrived(IReadOnlyList<SearchResult> batch)
    {
        var applied = _sorting.Applied;
        var order = Comparer<SearchResult>.Create(applied.Compare);
        return batch
            .Where(r => _searchedAssemblies.Contains(r.Node.Assembly))
            .Order(order)
            .ToList();
    }

    /// <summary>Merges the sorted <paramref name="arrived"/> into the sorted rows, each after the
    /// rows it ties with, filling from the end so no row moves more than once.</summary>
    private void MergeIntoRows(List<SearchResult> arrived)
    {
        var rows = _rows;
        var applied = _sorting.Applied;
        var kept = rows.Count - 1;
        var next = arrived.Count - 1;
        rows.AddRange(arrived);
        for (var at = rows.Count - 1; next >= 0; at--)
        {
            var keptGoesLast = kept >= 0 && applied.Compare(rows[kept], arrived[next]) > 0;
            if (keptGoesLast)
            {
                rows[at] = rows[kept];
                kept--;
            }
            else
            {
                rows[at] = arrived[next];
                next--;
            }
        }
    }

    public void Select(SearchResult row)
    {
        ItemSelections.Select(Selection, row, _rows);
    }

    public void Move(int by)
    {
        ItemSelections.Move(Selection, by, _rows);
    }

    /// <summary>Asks for the results by <paramref name="sort"/>, ascending, then descending when they already are,
    /// then by relevance again: the header shows it from now on. Returns what <see cref="Searches.SortAsync"/> needs to
    /// sort the results into it, which a newer sort or a new search cancels.</summary>
    public SearchSortTicket StartSort(SearchSort sort)
    {
        var asked = _sorting.Asked;
        if (asked.Sort == sort && asked.Descending)
        {
            sort = SearchSort.Relevance;
        }

        var order = new SearchOrder { Sort = sort, Descending = asked.Sort == sort && sort != SearchSort.Relevance };
        return _sorting.Start(order, _rows);
    }

    /// <summary>Shows <paramref name="sorted"/>, the rows as of <see cref="RowsVersion"/> in the order
    /// <paramref name="sorting"/> asked for, keeping the selection in view, unless a newer order was asked for
    /// meanwhile, or the search started again: then they are dropped.</summary>
    public void TakeSorted(SearchSortTicket sorting, List<SearchResult> sorted)
    {
        if (!_sorting.TryFinish(sorting))
        {
            return;
        }

        _rows.Clear();
        _rows.AddRange(sorted);
        ItemSelections.Reveal(Selection, _rows);
    }
}
