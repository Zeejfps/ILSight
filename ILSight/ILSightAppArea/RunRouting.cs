namespace ILSight.ILSightAppArea;

/// <summary>Decides where a run's arguments go: this run takes the role of receiving the others' arguments when it is
/// free; else, asked for a new window, it stands by to take the role later; else it hands its arguments to the run
/// holding the role, and stands by when that run doesn't take them. A hand-over whose answer is lost is sent again, up
/// to <see cref="MaxSends"/> times, under one request id the receiver takes once: the arguments are applied once and
/// no second window opens; still unanswered after the last send, the run says so and ends. Reaches the role, the
/// request id and the hand-over through <see cref="IPorts"/>.</summary>
public sealed class RunRouting(RunRouting.IPorts ports)
{
    /// <summary>How many times a run sends its arguments while the holder's answer is unknown.</summary>
    public const int MaxSends = 3;

    public interface IPorts
    {
        /// <summary>Takes the role when it is free, holding it from then on; false when another run holds it.</summary>
        Task<bool> TryClaimAsync(CancellationToken ct);

        /// <summary>A new id for a hand-over, never given out again.</summary>
        Guid NewRequestId();

        /// <summary>Hands <paramref name="arguments"/>, as request <paramref name="requestId"/>, to the run holding the
        /// role: whether it took them, didn't, or never answered.</summary>
        Task<HandOverAnswer> SendAsync(RunArguments arguments, Guid requestId, CancellationToken ct);

        /// <summary>Tells the user the running app never confirmed it took <paramref name="arguments"/>, and that no
        /// second window opened.</summary>
        void SayUnconfirmed(RunArguments arguments);
    }

    /// <summary>Where <paramref name="arguments"/> go: this run receives, they were handed over, this run stands by,
    /// or the hand-over stayed unconfirmed. A run asking for a <paramref name="newInstance"/> never sends, nor does one
    /// that took the role.</summary>
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

        return await HandOverAsync(arguments, ct);
    }

    private async Task<RunRoute> HandOverAsync(RunArguments arguments, CancellationToken ct)
    {
        var requestId = ports.NewRequestId();
        for (var sends = 0; sends < MaxSends; sends++)
        {
            var answer = await ports.SendAsync(arguments, requestId, ct);
            if (answer == HandOverAnswer.Taken)
            {
                return RunRoute.HandedOver;
            }

            if (answer == HandOverAnswer.NotTaken)
            {
                return RunRoute.StandsBy;
            }
        }

        ports.SayUnconfirmed(arguments);
        return RunRoute.Unconfirmed;
    }
}
