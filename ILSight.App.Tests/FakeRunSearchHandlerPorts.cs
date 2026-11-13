using System.Runtime.CompilerServices;
using ILSight.Core;
using ILSight.SearchArea;

namespace ILSight.App.Tests;

/// <summary>Searches nothing: finds <see cref="Found"/>, one entry at a time, for any query; keeps the problems
/// reported.</summary>
internal sealed class FakeRunSearchHandlerPorts : RunSearchHandler.IPorts
{
    public required IReadOnlyList<Outcome<IReadOnlyList<SearchFind>>> Found { get; init; }

    /// <summary>What the search throws before it finds anything, as a fault none of its answers covers; none when
    /// null.</summary>
    public Exception? Throws { get; init; }

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public async IAsyncEnumerable<Outcome<IReadOnlyList<SearchFind>>> SearchAsync(SearchQuery query, IReadOnlyList<LoadedModule> within,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var throws = Throws;
        if (throws is not null)
        {
            throw throws;
        }

        foreach (var found in Found)
        {
            await Task.CompletedTask;
            yield return found;
        }
    }
}
