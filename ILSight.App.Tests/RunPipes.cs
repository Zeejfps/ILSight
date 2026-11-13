using System.Threading.Channels;
using ILSight.ILSightAppArea;

namespace ILSight.App.Tests;

/// <summary>The role and the hand-over as a run reaches them (<see cref="RunRoutingAdapter"/>), awaited for the
/// tests that drive the real pipes.</summary>
internal static class RunPipes
{
    /// <summary>A receiver of the role at <paramref name="address"/>; null when another run holds it.</summary>
    public static Task<RunArgumentsReceiver?> ClaimAsync(RunArgumentsAddress address)
    {
        return RunArgumentsReceiver.TryClaimAsync(address, Channel.CreateUnbounded<RunArguments>(), CancellationToken.None);
    }

    /// <summary>Whether the run holding the role at <paramref name="address"/> took <paramref name="arguments"/>, sent once
    /// as a request of their own.</summary>
    public static async Task<bool> TrySendAsync(RunArgumentsAddress address, RunArguments arguments)
    {
        var answer = await SendAsync(address, arguments, Guid.NewGuid());
        return answer == HandOverAnswer.Taken;
    }

    /// <summary>What the run holding the role at <paramref name="address"/> answered to <paramref name="arguments"/>, sent
    /// as request <paramref name="requestId"/>.</summary>
    public static Task<HandOverAnswer> SendAsync(RunArgumentsAddress address, RunArguments arguments, Guid requestId)
    {
        return new RunRoutingAdapter(address, Channel.CreateUnbounded<RunArguments>()).SendAsync(arguments, requestId, CancellationToken.None);
    }
}
