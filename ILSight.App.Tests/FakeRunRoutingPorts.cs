using ILSight.ILSightAppArea;

namespace ILSight.App.Tests;

/// <summary>Answers the claim as the test set and each send with the next of <see cref="Answers"/> (the last again once
/// they run out); keeps what was sent, under which request, and what was said.</summary>
internal sealed class FakeRunRoutingPorts : RunRouting.IPorts
{
    public required bool Claims { get; init; }

    public IReadOnlyList<HandOverAnswer> Answers { get; init; } = [HandOverAnswer.NotTaken];

    public List<RunArguments> Sent { get; } = [];

    public List<Guid> Requests { get; } = [];

    public List<RunArguments> SaidUnconfirmed { get; } = [];

    public Task<bool> TryClaimAsync(CancellationToken ct)
    {
        return Task.FromResult(Claims);
    }

    public Guid NewRequestId()
    {
        return Guid.NewGuid();
    }

    public Task<HandOverAnswer> SendAsync(RunArguments arguments, Guid requestId, CancellationToken ct)
    {
        Sent.Add(arguments);
        Requests.Add(requestId);
        return Task.FromResult(Answers[Math.Min(Sent.Count, Answers.Count) - 1]);
    }

    public void SayUnconfirmed(RunArguments arguments)
    {
        SaidUnconfirmed.Add(arguments);
    }
}
