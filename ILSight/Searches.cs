using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight;

/// <summary>Running a <see cref="Search"/>: starting it once the query settled, taking in what it finds,
/// and sorting and picking from what it found.</summary>
public static class Searches
{
    /// <summary>A search finding nodes of the browse tree <paramref name="session"/>, telling the user with
    /// <paramref name="reportError"/> when it fails.</summary>
    public static Search CreateSearch(BackgroundWork work, Action<AppProblem> reportError, BrowseSession session)
    {
        return new Search
        {
            Work = work,
            ReportError = reportError,
            Stream = (_, query, within, cancellationToken) => AssemblySearch.SearchAsync(session, within, query, cancellationToken),
        };
    }

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

    private static bool IsQueryChanged(Search search, Search.Searched searched)
    {
        return search.Typed.Text != searched.Query || search.Kinds != searched.Kinds;
    }

    private static double RestartWhenSettled(Search search, AssemblyList assemblies, double now, Search.Searched? searched)
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
        CancelRuns(search);
        var searched = new Search.Searched(search.Typed.Text, search.Kinds, assemblies.Version);
        search.LastSearched = searched;
        search.Rows.Clear();
        ItemSelections.Clear(search.Selection);
        var listed = assemblies.Assemblies;
        search.SearchedAssemblies.Clear();
        search.SearchedAssemblies.UnionWith(listed);
        Start(search, assemblies, searched, listed);
    }

    private static void CancelRuns(Search search)
    {
        foreach (var run in search.Runs)
        {
            run.Cancel();
        }

        search.Runs.Clear();
    }

    private static void Extend(Search search, AssemblyList assemblies, Search.Searched searched)
    {
        searched = searched with { Version = assemblies.Version };
        search.LastSearched = searched;
        var listed = assemblies.Assemblies;
        DropClosed(search, listed);
        var added = MarkSearched(search, listed);
        if (added.Count > 0)
        {
            Start(search, assemblies, searched, added);
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

    private static void Start(Search search, AssemblyList assemblies, Search.Searched searched, IEnumerable<LoadedModule> within)
    {
        var query = SearchQueries.ParseQuery(searched.Query, searched.Kinds);
        search.InvalidRegex = query.Matcher as SearchMatcher.InvalidRegex;
        if (!query.CanFind)
        {
            return;
        }

        var work = new CancellationTokenSource();
        search.Runs.Add(work);
        var batches = search.Stream(assemblies, query, within, work.Token);
        Background.Start(search.Work, nameof(RunAsync), RunAsync(search, batches, work), search.ReportError);
    }

    private static async Task RunAsync(Search search, IAsyncEnumerable<IReadOnlyList<SearchResult>> batches, CancellationTokenSource work)
    {
        try
        {
            await foreach (var batch in batches)
            {
                Deliver(search, batch, work);
            }
        }
        catch (OperationCanceledException) when (work.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            ReportFailure(search, work, e);
        }
        search.Runs.Remove(work);
        work.Dispose();
    }

    private static bool IsCurrent(Search search, CancellationTokenSource work)
    {
        return search.Runs.Contains(work);
    }

    /// <summary>Reports why the run <paramref name="work"/> failed, unless another search replaced it.</summary>
    private static void ReportFailure(Search search, CancellationTokenSource work, Exception error)
    {
        if (!IsCurrent(search, work))
        {
            return;
        }

        search.ReportError(new AppProblem.SearchFailed(error));
    }

    private static void Deliver(Search search, IReadOnlyList<SearchResult> batch, CancellationTokenSource work)
    {
        if (!IsCurrent(search, work))
        {
            return;
        }

        var arrived = SortArrived(search, batch);
        MergeIntoRows(search, arrived);
    }

    /// <summary>The hits of <paramref name="batch"/> still wanted, in order. The order reads the sort of the
    /// moment, so a comparer is made per batch.</summary>
    private static List<SearchResult> SortArrived(Search search, IReadOnlyList<SearchResult> batch)
    {
        var order = Comparer<SearchResult>.Create((a, b) => Compare(search, a, b));
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
        var kept = rows.Count - 1;
        var next = arrived.Count - 1;
        rows.AddRange(arrived);
        for (var at = rows.Count - 1; next >= 0; at--)
        {
            var keptGoesLast = kept >= 0 && Compare(search, rows[kept], arrived[next]) > 0;
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
            return SearchQueries.AddScope(search.Query, NamespaceScope, BrowseNodes.GetFullName(node));
        }

        return SearchQueries.AddScope(search.Query, AssemblyScope, node.Assembly.Name);
    }

    /// <summary>Sorts the results by <paramref name="sort"/>, ascending, then descending when they
    /// already are, then by relevance again, keeping the selection in view.</summary>
    public static void SortBy(Search search, SearchSort sort)
    {
        if (search.Sort == sort && search.Descending)
        {
            sort = SearchSort.Relevance;
        }

        search.Descending = search.Sort == sort && sort != SearchSort.Relevance;
        search.Sort = sort;
        search.Rows.Sort((a, b) => Compare(search, a, b));
        ItemSelections.Reveal(search.Selection, search.Rows);
    }

    private static int Compare(Search search, SearchResult a, SearchResult b)
    {
        var order = CompareAscending(search, a, b);
        return search.Descending ? -order : order;
    }

    private static int CompareAscending(Search search, SearchResult a, SearchResult b)
    {
        return search.Sort switch
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
