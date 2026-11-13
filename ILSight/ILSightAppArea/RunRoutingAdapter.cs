namespace ILSight.ILSightAppArea;

/// <summary>Reaches the role and the hand-over at <paramref name="address"/> for <see cref="RunRouting"/>: the claim
/// starts a receiver and awaits its role thread's answer, holding the receiver it claimed (<see cref="Claimed"/>); the
/// send hands the arguments over the pipe, a run that can't be reached in time, refuses or breaks the hand-over
/// answered as not taken. Decides nothing.</summary>
public sealed class RunRoutingAdapter(RunArgumentsAddress address) : RunRouting.IPorts
{
    /// <summary>The receiver <see cref="TryClaimAsync"/> claimed; null until it claimed one.</summary>
    public RunArgumentsReceiver? Claimed { get; private set; }

    public async Task<bool> TryClaimAsync(CancellationToken ct)
    {
        var claimed = await RunArgumentsReceiver.TryClaimAsync(address, ct);
        Claimed = claimed;
        return claimed is not null;
    }

    public async Task<bool> TrySendAsync(RunArguments arguments, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RunArgumentsPipe.HandOverTimeout);
        try
        {
            return await RunArgumentsPipe.SendAsync(address, arguments, timeout.Token);
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
