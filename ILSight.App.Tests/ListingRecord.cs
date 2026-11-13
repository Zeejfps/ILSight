using System.Collections.Concurrent;
using ILSight.AssemblyArea;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Keeps every path the reopening of a list asked the real adapter to list (<see cref="Over"/>), in the order
/// asked, for a test that a list's files were never read: the app built through the root with it, as it is otherwise,
/// the real adapter answering in its own time.</summary>
internal sealed class ListingRecord
{
    private readonly ConcurrentQueue<AssemblyPath> _asked = new();

    /// <summary>The paths asked to be listed, so far.</summary>
    public IReadOnlyCollection<AssemblyPath> Asked => _asked;

    /// <summary>The port the root hands the reopening: <paramref name="real"/>, the real adapter, noting what it is asked
    /// to list first.</summary>
    public OpenAssembliesAsReloadHandler.IPorts Over(OpenAssembliesAsReloadHandler.IPorts real)
    {
        return new Recording(real, this);
    }

    private sealed class Recording(OpenAssembliesAsReloadHandler.IPorts real, ListingRecord record) : OpenAssembliesAsReloadHandler.IPorts
    {
        public void ReportError(AppProblem problem)
        {
            real.ReportError(problem);
        }

        public Task<Outcome<LoadedAssembly?>> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct)
        {
            foreach (var path in paths)
            {
                record._asked.Enqueue(path);
            }

            return real.OpenFilesAsync(paths, stop, ct);
        }
    }
}
