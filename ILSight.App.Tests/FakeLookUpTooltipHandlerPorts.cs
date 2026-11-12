using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Describes nothing itself: answers every reference as the test says; keeps the references asked
/// about.</summary>
internal sealed class FakeLookUpTooltipHandlerPorts : LookUpTooltipHandler.IPorts
{
    public required Outcome<Lookup<CodeTooltip>> Answer { get; init; }

    public List<CodeReference.Entity> Asked { get; } = [];

    public Task<Outcome<Lookup<CodeTooltip>>> DescribeReferenceAsync(CodeReference.Entity reference, CancellationToken ct)
    {
        Asked.Add(reference);
        return Task.FromResult(Answer);
    }
}
