using System.Runtime.CompilerServices;
using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>Reaches the app's error reports, and searches the browse tree's assemblies off the UI thread, for
/// <see cref="RunSearchHandler"/>: each batch is found on the thread pool and brought back on the UI thread. The core
/// answers each assembly the library failed on as a find of its own (<see cref="SearchFind.Unsearched"/>), so the
/// sequence never ends <see cref="Outcome{T}.Failed"/> here: what else the search throws is a bug, which goes on to the
/// workflow's edge, as does the cancellation of the token the call was given.</summary>
public sealed class RunSearchHandlerAdapter(ErrorLog errors, BrowseSession session) : RunSearchHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public async IAsyncEnumerable<Outcome<IReadOnlyList<SearchFind>>> SearchAsync(SearchQuery query, IReadOnlyList<LoadedModule> within,
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
            if (next is Outcome<IReadOnlyList<SearchFind>>.Failed)
            {
                yield break;
            }
        }
    }

    private static async Task<Outcome<IReadOnlyList<SearchFind>>?> ReadNextAsync(IAsyncEnumerator<IReadOnlyList<SearchFind>> batches,
        CancellationToken ct)
    {
        var moved = await Background.Wait(batches.MoveNextAsync().AsTask());
        ct.ThrowIfCancellationRequested();
        return moved ? new Outcome<IReadOnlyList<SearchFind>>.Ok(batches.Current) : null;
    }
}
