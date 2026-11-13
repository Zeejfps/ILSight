using ILSight.ILSightAppArea;

namespace ILSight.App.Tests;

/// <summary>The role and the hand-over as a run reaches them (<see cref="RunRoutingAdapter"/>), awaited for the
/// tests that drive the real pipes.</summary>
internal static class RunPipes
{
    /// <summary>A receiver of the role at <paramref name="address"/>; null when another run holds it.</summary>
    public static RunArgumentsReceiver? Claim(RunArgumentsAddress address)
    {
        return TestWaits.Wait(() => RunArgumentsReceiver.TryClaimAsync(address, CancellationToken.None));
    }

    /// <summary>Whether the run holding the role at <paramref name="address"/> took <paramref name="arguments"/>.</summary>
    public static bool Send(RunArgumentsAddress address, RunArguments arguments)
    {
        return TestWaits.Wait(() => new RunRoutingAdapter(address).TrySendAsync(arguments, CancellationToken.None));
    }

    public static Task<bool> SendAsync(RunArgumentsAddress address, RunArguments arguments)
    {
        return new RunRoutingAdapter(address).TrySendAsync(arguments, CancellationToken.None);
    }
}
