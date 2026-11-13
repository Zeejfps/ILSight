using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Finds <see cref="Found"/> for any reference, doing <see cref="WhileLooking"/> meanwhile; keeps the
/// references looked up and the problems reported and the refusals shown.</summary>
internal sealed class FakeNavigateHandlerPorts : NavigateHandler.IPorts
{
    public required Outcome<Lookup<BrowseNode>> Found { get; init; }

    public Action WhileLooking { get; init; } = static () => { };

    public List<CodeReference.Navigable> LookedUp { get; } = [];

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

    public Task<Outcome<Lookup<BrowseNode>>> LookUpReferenceAsync(CodeReference.Navigable reference, CancellationToken ct)
    {
        LookedUp.Add(reference);
        WhileLooking();
        return Task.FromResult(Found);
    }
}
