using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.MetadataArea;

namespace ILSight.App.Tests;

/// <summary>Finds <see cref="Found"/> for any reference, doing <see cref="WhileLooking"/> meanwhile; keeps the
/// problems reported.</summary>
internal sealed class FakeShowInMetadataHandlerPorts : ShowInMetadataHandler.IPorts
{
    public required Outcome<Lookup<MetadataTarget>> Found { get; init; }

    public Action WhileLooking { get; init; } = static () => { };

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<Lookup<MetadataTarget>>> LookUpMetadataRowAsync(CodeReference.Navigable reference, CancellationToken ct)
    {
        WhileLooking();
        return Task.FromResult(Found);
    }
}
