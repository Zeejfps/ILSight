using System.Runtime.CompilerServices;
using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>Searches the browse tree's assemblies off the UI thread, for <see cref="RunSearchHandler"/>: each batch is
/// found on the thread pool and brought back on the UI thread. The core answers each assembly the library failed on as
/// a find of its own (<see cref="SearchFind.Unsearched"/>): what else the search throws is a bug, which goes on to the
/// workflow's edge, as does the cancellation of the token the call was given.</summary>
public sealed class RunSearchHandlerAdapter(BrowseSession session) : RunSearchHandler.IPorts
{
    public async IAsyncEnumerable<IReadOnlyList<SearchFind>> SearchAsync(SearchQuery query, IReadOnlyList<LoadedModule> within,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var batches = AssemblySearch.SearchAsync(session, within, query, ct).GetAsyncEnumerator(ct);
        while (true)
        {
            var moved = await Background.Wait(batches.MoveNextAsync().AsTask());
            ct.ThrowIfCancellationRequested();
            if (!moved)
            {
                yield break;
            }

            yield return batches.Current;
        }
    }
}
