using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>Runs a search's run through <see cref="IPorts"/>, without holding up the screen, merging what it finds into
/// the search's rows as it comes in, while no new query replaced it. A run whose workflow fails is settled failed by the
/// edge that starts it (<see cref="SearchResults.FailRun"/>).</summary>
public sealed class RunSearchHandler(RunSearchHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>The hits of <paramref name="query"/> in <paramref name="within"/>, in batches as they are found, with
        /// each assembly that couldn't be searched, worked out without holding up the screen; the cancellation of
        /// <paramref name="ct"/> is let through.</summary>
        IAsyncEnumerable<IReadOnlyList<SearchFind>> SearchAsync(SearchQuery query, IReadOnlyList<LoadedModule> within, CancellationToken ct);
    }

    /// <summary>Runs <paramref name="run"/>, delivering what it finds to <paramref name="results"/>, then ends it.
    /// <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the run's work.</summary>
    public async Task HandleAsync(SearchResults results, SearchRun run, CancellationToken ct)
    {
        await foreach (var found in ports.SearchAsync(run.Query, run.Within, ct))
        {
            results.Deliver(found, run);
        }

        results.EndRun(run);
    }
}
