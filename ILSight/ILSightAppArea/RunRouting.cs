namespace ILSight.ILSightAppArea;

/// <summary>Decides where a run's arguments go: this run takes the role of receiving the others' arguments when it is
/// free; else, asked for a new window, it stands by to take the role later; else it hands its arguments to the run
/// holding the role, and stands by when that run doesn't take them. Reaches the role and the hand-over through
/// <see cref="IPorts"/>.</summary>
public sealed class RunRouting(RunRouting.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Takes the role when it is free, holding it from then on; false when another run holds it.</summary>
        Task<bool> TryClaimAsync(CancellationToken ct);

        /// <summary>Hands <paramref name="arguments"/> to the run holding the role; false when it doesn't take them in
        /// time.</summary>
        Task<bool> TrySendAsync(RunArguments arguments, CancellationToken ct);
    }

    /// <summary>Where <paramref name="arguments"/> go: this run receives, they were handed over, or this run stands by.
    /// A run asking for a <paramref name="newInstance"/> never sends, nor does one that took the role.</summary>
    public async Task<RunRoute> RouteAsync(RunArguments arguments, bool newInstance, CancellationToken ct)
    {
        if (await ports.TryClaimAsync(ct))
        {
            return RunRoute.Receives;
        }

        if (newInstance)
        {
            return RunRoute.StandsBy;
        }

        if (await ports.TrySendAsync(arguments, ct))
        {
            return RunRoute.HandedOver;
        }

        return RunRoute.StandsBy;
    }
}
