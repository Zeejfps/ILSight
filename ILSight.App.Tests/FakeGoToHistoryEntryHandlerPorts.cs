using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Finds <see cref="Found"/> for any place, doing <see cref="WhileLooking"/> meanwhile; keeps the
/// places looked up and the problems reported and the refusals shown.</summary>
internal sealed class FakeGoToHistoryEntryHandlerPorts : GoToHistoryEntryHandler.IPorts
{
    public required Outcome<Lookup<BrowseNode>> Found { get; init; }

    public Action WhileLooking { get; init; } = static () => { };

    public List<NodePath> LookedUp { get; } = [];

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

    public Task<Outcome<Lookup<BrowseNode>>> LookUpListedPlaceAsync(NodePath place, CancellationToken ct)
    {
        LookedUp.Add(place);
        WhileLooking();
        return Task.FromResult(Found);
    }
}
