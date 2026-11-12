using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Opens nothing and finds <see cref="Found"/>, doing <see cref="WhileOpening"/> meanwhile; keeps the
/// problems reported.</summary>
internal sealed class FakeOpenAndShowIdHandlerPorts : OpenAndShowIdHandler.IPorts
{
    public required Outcome<BrowseNode?> Found { get; init; }

    public Action WhileOpening { get; init; } = static () => { };

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<BrowseNode?>> OpenAndFindIdAsync(IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken ct)
    {
        WhileOpening();
        return Task.FromResult(Found);
    }
}
