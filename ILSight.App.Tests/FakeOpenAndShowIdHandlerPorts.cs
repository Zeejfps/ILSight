using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Opens nothing and finds <see cref="Found"/>, doing <see cref="WhileOpening"/> meanwhile; keeps the
/// problems reported and the refusals shown.</summary>
internal sealed class FakeOpenAndShowIdHandlerPorts : OpenAndShowIdHandler.IPorts
{
    public required Outcome<Lookup<BrowseNode>> Found { get; init; }

    public Action WhileOpening { get; init; } = static () => { };

    public List<AppProblem> Reported { get; } = [];

    public List<Refusal> Refused { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public void ShowRefusal(Refusal refusal)
    {
        Refused.Add(refusal);
    }

    public Task<Outcome<Lookup<BrowseNode>>> OpenAndFindIdAsync(IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken ct)
    {
        WhileOpening();
        return Task.FromResult(Found);
    }
}
