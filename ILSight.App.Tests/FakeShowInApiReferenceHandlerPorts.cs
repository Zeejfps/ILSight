using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Finds <see cref="Page"/> for any reference; keeps the problems reported.</summary>
internal sealed class FakeShowInApiReferenceHandlerPorts : ShowInApiReferenceHandler.IPorts
{
    public required Outcome<string?> Page { get; init; }

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<string?>> LookUpApiReferencePageAsync(CodeReference.Entity reference, CancellationToken ct)
    {
        return Task.FromResult(Page);
    }
}
