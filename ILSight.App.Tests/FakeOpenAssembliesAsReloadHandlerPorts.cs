using ILSight.Core;
using ILSight.AssemblyArea;

namespace ILSight.App.Tests;

/// <summary>Answers <see cref="OpenAssembliesAsReloadHandler"/>'s opening with what the test set, doing
/// <see cref="WhileWorking"/> before it answers; keeps the stop tokens it was asked with and the problems
/// reported.</summary>
internal sealed class FakeOpenAssembliesAsReloadHandlerPorts : OpenAssembliesAsReloadHandler.IPorts
{
    public required Outcome<LoadedAssembly?> Opened { get; init; }

    public Action WhileWorking { get; init; } = static () => { };

    public List<CancellationToken> Stops { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<LoadedAssembly?>> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct)
    {
        Stops.Add(stop);
        WhileWorking();
        return Task.FromResult(Opened);
    }
}
