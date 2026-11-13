using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>Runs a search's run through <see cref="IPorts"/>, without holding up the screen, merging what it finds into
/// the search's rows as it comes in, while no new query replaced it; when it fails, it is reported, unless a new query
/// replaced it.</summary>
public sealed class RunSearchHandler(RunSearchHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user a search failed.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The hits of <paramref name="query"/> in <paramref name="within"/>, in batches as they are found, with
        /// each assembly that couldn't be searched, worked out without holding up the screen. Working it out that failed ends the sequence with
        /// <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let through.</summary>
        IAsyncEnumerable<Outcome<IReadOnlyList<SearchFind>>> SearchAsync(SearchQuery query, IReadOnlyList<LoadedModule> within, CancellationToken ct);
    }

    /// <summary>Runs <paramref name="run"/>, delivering what it finds to <paramref name="results"/>, then ends it.
    /// <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the run's work.</summary>
    public async Task HandleAsync(SearchResults results, SearchRun run, CancellationToken ct)
    {
        await foreach (var found in ports.SearchAsync(run.Query, run.Within, ct))
        {
            var error = found.Match<Exception?>(
                ok =>
                {
                    results.Deliver(ok.Value, run);
                    return null;
                },
                static failed => failed.Error);
            if (error is not null)
            {
                ReportFailure(results, run, error);
                break;
            }
        }

        results.EndRun(run);
    }

    /// <summary>Reports why <paramref name="run"/> failed, unless a new query replaced it.</summary>
    private void ReportFailure(SearchResults results, SearchRun run, Exception error)
    {
        if (!results.IsCurrent(run))
        {
            return;
        }

        ports.ReportError(new AppProblem.SearchFailed(error));
    }
}
