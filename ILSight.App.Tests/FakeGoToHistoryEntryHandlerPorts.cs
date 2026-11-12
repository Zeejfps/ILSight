using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Finds <see cref="Found"/> for any place, doing <see cref="WhileLooking"/> meanwhile; keeps the
/// places looked up and the problems reported.</summary>
internal sealed class FakeGoToHistoryEntryHandlerPorts : GoToHistoryEntryHandler.IPorts
{
    public required Outcome<BrowseNode?> Found { get; init; }

    public Action WhileLooking { get; init; } = static () => { };

    public List<NodePath> LookedUp { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<BrowseNode?>> LookUpListedPlaceAsync(NodePath place, CancellationToken ct)
    {
        LookedUp.Add(place);
        WhileLooking();
        return Task.FromResult(Found);
    }
}
