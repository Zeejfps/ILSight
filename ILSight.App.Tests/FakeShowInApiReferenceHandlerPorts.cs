using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Finds <see cref="Page"/> for any reference; keeps the problems reported and the refusals shown.</summary>
internal sealed class FakeShowInApiReferenceHandlerPorts : ShowInApiReferenceHandler.IPorts
{
    public required Outcome<Lookup<string>> Page { get; init; }

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

    public Task<Outcome<Lookup<string>>> LookUpApiReferencePageAsync(CodeReference.Entity reference, CancellationToken ct)
    {
        return Task.FromResult(Page);
    }
}
