using ILSight.ILSightAppArea;

namespace ILSight.App.Tests;

/// <summary>Answers the claim and the send as the test set; keeps what was sent.</summary>
internal sealed class FakeRunRoutingPorts : RunRouting.IPorts
{
    public required bool Claims { get; init; }

    public bool Takes { get; init; }

    public List<RunArguments> Sent { get; } = [];

    public Task<bool> TryClaimAsync(CancellationToken ct)
    {
        return Task.FromResult(Claims);
    }

    public Task<bool> TrySendAsync(RunArguments arguments, CancellationToken ct)
    {
        Sent.Add(arguments);
        return Task.FromResult(Takes);
    }
}
