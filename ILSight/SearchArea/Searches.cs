using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>Running a <see cref="Search"/>: starting it once the query settled, taking in what it finds,
/// and sorting and picking from what it found.</summary>
public static class Searches
{
    private const double DebounceSeconds = 0.15;
    private const string NamespaceScope = "innamespace";
    private const string AssemblyScope = "inassembly";

    /// <summary>Starts a search when the query settled, or searches assemblies opened since. Does
    /// nothing unless the results are <paramref name="shown"/>. Returns the seconds left before a
    /// typed query settles, or 0. Runs every frame, so no lambda belongs in it.</summary>
    public static double Update(Search search, AssemblyList assemblies, double now, bool shown)
    {
        search.Typed.Note(search.Query, now);
        if (!shown)
        {
            return 0;
        }

        var searched = search.LastSearched;
        if (searched is null || IsQueryChanged(search, searched))
        {
            return RestartWhenSettled(search, assemblies, now, searched);
        }

        if (assemblies.Version != searched.Version)
        {
            Extend(search, assemblies, searched);
        }

        return 0;
    }

    private static bool IsQueryChanged(Search search, SearchedQuery searched)
    {
        return search.Typed.Text != searched.Query || search.Kinds != searched.Kinds;
    }

    private static double RestartWhenSettled(Search search, AssemblyList assemblies, double now, SearchedQuery? searched)
    {
        var remaining = search.Typed.CalculateSecondsToSettle(now, DebounceSeconds);
        if (remaining > 0 && searched is not null && search.Kinds == searched.Kinds)
        {
            return remaining;
        }

        Restart(search, assemblies);
        return 0;
    }

    private static void Restart(Search search, AssemblyList assemblies)
    {
        search.Runs.CancelAll();
        var searched = new SearchedQuery(search.Typed.Text, search.Kinds, assemblies.Version);
        search.LastSearched = searched;
        search.Rows.Clear();
        search.Sorting.NoteRowsEmptied();
        ItemSelections.Clear(search.Selection);
        var listed = assemblies.Assemblies;
        search.SearchedAssemblies.Clear();
        search.SearchedAssemblies.UnionWith(listed);
        Ask(search, searched, listed);
    }

    private static void Extend(Search search, AssemblyList assemblies, SearchedQuery searched)
    {
        searched = searched with { Version = assemblies.Version };
        search.LastSearched = searched;
        var listed = assemblies.Assemblies;
        DropClosed(search, listed);
        var added = MarkSearched(search, listed);
        if (added.Count > 0)
        {
            Ask(search, searched, added);
        }
    }

    private static void DropClosed(Search search, IReadOnlyList<LoadedModule> listed)
    {
        var closed = search.SearchedAssemblies.RemoveWhere(a => !listed.Contains(a));
        if (closed == 0)
        {
            return;
        }

        search.Rows.RemoveAll(r => !search.SearchedAssemblies.Contains(r.Node.Assembly));
        search.Sorting.NoteRowsChanged();
        ItemSelections.DropIfGone(search.Selection, search.Rows);
    }

    private static List<LoadedModule> MarkSearched(Search search, IReadOnlyList<LoadedModule> listed)
    {
        var added = new List<LoadedModule>();
        foreach (var assembly in listed)
        {
            if (search.SearchedAssemblies.Add(assembly))
            {
                added.Add(assembly);
            }
        }
        return added;
    }

    /// <summary>Asks for <paramref name="searched"/> to be run over <paramref name="within"/>, unless it can find
    /// nothing; the edge starts it once the frame is drawn (<see cref="RunSearchHandler"/>).</summary>
    private static void Ask(Search search, SearchedQuery searched, IReadOnlyList<LoadedModule> within)
    {
        var query = SearchQueries.ParseQuery(searched.Query, searched.Kinds);
        search.InvalidRegex = query.Matcher as SearchMatcher.InvalidRegex;
        if (!query.CanFind)
        {
            return;
        }

        search.Runs.Ask(new SearchRun { Query = query, Within = [.. within] });
    }

    /// <summary>Merges the hits of <paramref name="batch"/> that are still wanted into the rows, in their order, unless
    /// a new query replaced <paramref name="run"/>, which found them.</summary>
    public static void Deliver(Search search, IReadOnlyList<SearchResult> batch, SearchRun run)
    {
        if (!search.Runs.IsCurrent(run))
        {
            return;
        }

        var arrived = SortArrived(search, batch);
        MergeIntoRows(search, arrived);
        search.Sorting.NoteRowsChanged();
    }

    /// <summary>The hits of <paramref name="batch"/> still wanted, in the order the rows are in, which a comparer
    /// is made for per batch.</summary>
    private static List<SearchResult> SortArrived(Search search, IReadOnlyList<SearchResult> batch)
    {
        var applied = search.Sorting.Applied;
        var order = Comparer<SearchResult>.Create((a, b) => Compare(applied, a, b));
        return batch
            .Where(r => search.SearchedAssemblies.Contains(r.Node.Assembly))
            .Order(order)
            .ToList();
    }

    /// <summary>Merges the sorted <paramref name="arrived"/> into the sorted rows, each after the
    /// rows it ties with, filling from the end so no row moves more than once.</summary>
    private static void MergeIntoRows(Search search, List<SearchResult> arrived)
    {
        var rows = search.Rows;
        var applied = search.Sorting.Applied;
        var kept = rows.Count - 1;
        var next = arrived.Count - 1;
        rows.AddRange(arrived);
        for (var at = rows.Count - 1; next >= 0; at--)
        {
            var keptGoesLast = kept >= 0 && Compare(applied, rows[kept], arrived[next]) > 0;
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

    public static void Select(Search search, SearchResult row)
    {
        ItemSelections.Select(search.Selection, row, search.Rows);
    }

    public static void Move(Search search, int by)
    {
        ItemSelections.Move(search.Selection, by, search.Rows);
    }

    /// <summary>Empties the query, and drops a focus asked for that the box has not taken yet.</summary>
    public static void Clear(Search search)
    {
        search.Query = "";
        search.IsFocusPending = false;
    }

    /// <summary>True once after a focus was asked for, unless cleared since: the box should take focus.</summary>
    public static bool TryTakeFocus(Search search)
    {
        var focus = search.IsFocusPending;
        search.IsFocusPending = false;
        return focus;
    }

    /// <summary>Keeps the query to <paramref name="node"/>'s assembly, or to the namespace it is.</summary>
    public static void ScopeTo(Search search, BrowseNode node)
    {
        search.Query = CreateScopedQuery(search, node);
    }

    private static string CreateScopedQuery(Search search, BrowseNode node)
    {
        if (node.Subject is NodeSubject.Namespace)
        {
            return SearchQueries.AddScope(search.Query, NamespaceScope, node.FullName);
        }

        return SearchQueries.AddScope(search.Query, AssemblyScope, node.Assembly.Name);
    }

    /// <summary>Asks for the results by <paramref name="sort"/>, ascending, then descending when they already are,
    /// then by relevance again: the header shows it from now on. Returns what <see cref="SortAsync"/> needs to sort
    /// the results into it, which a newer sort or a new search cancels.</summary>
    public static SearchSortTicket StartSort(Search search, SearchSort sort)
    {
        var asked = search.Sorting.Asked;
        if (asked.Sort == sort && asked.Descending)
        {
            sort = SearchSort.Relevance;
        }

        var order = new SearchOrder { Sort = sort, Descending = asked.Sort == sort && sort != SearchSort.Relevance };
        return search.Sorting.Start(order, search.Rows);
    }

    /// <summary>Sorts the results into the order <paramref name="sorting"/> asked for, off the UI thread, and shows
    /// them so unless a newer order was asked for meanwhile, keeping the selection in view. Results that arrived or
    /// went while it sorted are sorted again with the rest. <paramref name="ct"/> is the sort's token, linked to what
    /// else may cancel it.</summary>
    public static async Task SortAsync(Search search, SearchSortTicket sorting, CancellationToken ct)
    {
        var version = sorting.RowsVersion;
        var sorted = await SortOffThreadAsync(sorting.Rows, sorting.Order, ct);
        while (version != search.Sorting.RowsVersion)
        {
            version = search.Sorting.RowsVersion;
            sorted = await SortOffThreadAsync(search.Rows.ToList(), sorting.Order, ct);
        }

        if (!search.Sorting.TryFinish(sorting))
        {
            return;
        }

        search.Rows.Clear();
        search.Rows.AddRange(sorted);
        ItemSelections.Reveal(search.Selection, search.Rows);
    }

    private static Task<List<SearchResult>> SortOffThreadAsync(IReadOnlyList<SearchResult> rows, SearchOrder order, CancellationToken ct)
    {
        return Task.Run(() => Sort(rows, order), ct);
    }

    /// <summary><paramref name="rows"/> in <paramref name="order"/>, as a list of their own.</summary>
    private static List<SearchResult> Sort(IReadOnlyList<SearchResult> rows, SearchOrder order)
    {
        var sorted = rows.ToList();
        sorted.Sort((a, b) => Compare(order, a, b));
        return sorted;
    }

    private static int Compare(SearchOrder order, SearchResult a, SearchResult b)
    {
        var compared = CompareAscending(order.Sort, a, b);
        return order.Descending ? -compared : compared;
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
