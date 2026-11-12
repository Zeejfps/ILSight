using System.Runtime.CompilerServices;
using ILSight.Core;

namespace ILSight;

/// <summary>Reaches the app's error reports, and searches the browse tree's assemblies off the UI thread, for
/// <see cref="RunSearchHandler"/>: each batch is found on the thread pool and brought back on the UI thread; the first
/// read that throws ends the sequence with what it threw, except the cancellation of the token the call was given,
/// which it lets through.</summary>
public sealed class RunSearchHandlerAdapter(ErrorLog errors, BrowseSession session) : RunSearchHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public async IAsyncEnumerable<Outcome<IReadOnlyList<SearchResult>>> SearchAsync(SearchQuery query, IReadOnlyList<LoadedModule> within,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var batches = AssemblySearch.SearchAsync(session, within, query, ct).GetAsyncEnumerator(ct);
        while (true)
        {
            var next = await ReadNextAsync(batches, ct);
            if (next is null)
            {
                yield break;
            }

            yield return next;
            if (next is Outcome<IReadOnlyList<SearchResult>>.Failed)
            {
                yield break;
            }
        }
    }

    /// <summary>The next batch of <paramref name="batches"/>, or what reading it threw; null at the end.</summary>
    private static async Task<Outcome<IReadOnlyList<SearchResult>>?> ReadNextAsync(IAsyncEnumerator<IReadOnlyList<SearchResult>> batches,
        CancellationToken ct)
    {
        var moved = await Background.Wait(batches.MoveNextAsync().AsTask());
        ct.ThrowIfCancellationRequested();
        return moved.Match<Outcome<IReadOnlyList<SearchResult>>?>(
            ok => ok.Value ? new Outcome<IReadOnlyList<SearchResult>>.Ok(batches.Current) : null,
            static failed => new Outcome<IReadOnlyList<SearchResult>>.Failed(failed.Error));
    }
}
