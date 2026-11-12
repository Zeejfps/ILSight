using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight.App.Tests;

/// <summary>Finds <see cref="Found"/> for any saved analyses, doing <see cref="WhileLooking"/> meanwhile; keeps the
/// problems reported.</summary>
internal sealed class FakeRestoreAnalysesHandlerPorts : RestoreAnalysesHandler.IPorts
{
    public required Outcome<List<AnalysisSymbol?>> Found { get; init; }

    public Action WhileLooking { get; init; } = static () => { };

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<List<AnalysisSymbol?>>> LookUpAnalyzedAsync(IReadOnlyList<CodeAnchor> analyzed, CancellationToken ct)
    {
        WhileLooking();
        return Task.FromResult(Found);
    }
}
