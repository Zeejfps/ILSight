using System.IO.Pipes;
using System.Threading.Channels;
using ILSight.CommandArea;

namespace ILSight.ILSightAppArea;

/// <summary>Reaches the role and the hand-over at <paramref name="address"/> for <see cref="RunRouting"/>: the claim
/// starts a receiver putting what later runs send in <paramref name="inbox"/>, which holds this run's own arguments
/// already, and awaits its role thread's answer, holding the receiver it claimed (<see cref="Claimed"/>); the
/// send hands the arguments over the pipe: a run that can't be reached in time, or refuses them, has not taken them; a
/// hand-over that breaks or times out once they were sent is unknown. Decides nothing.</summary>
public sealed class RunRoutingAdapter(RunArgumentsAddress address, Channel<RunArguments> inbox) : RunRouting.IPorts
{
    /// <summary>The receiver <see cref="TryClaimAsync"/> claimed; null until it claimed one.</summary>
    public RunArgumentsReceiver? Claimed { get; private set; }

    public async Task<bool> TryClaimAsync(CancellationToken ct)
    {
        var claimed = await RunArgumentsReceiver.TryClaimAsync(address, inbox, ct);
        Claimed = claimed;
        return claimed is not null;
    }

    public Guid NewRequestId()
    {
        return Guid.NewGuid();
    }

    public async Task<HandOverAnswer> SendAsync(RunArguments arguments, Guid requestId, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RunArgumentsPipe.HandOverTimeout);
        await using var pipe = RunArgumentsPipe.CreateClient(address);
        if (!await TryReachAsync(pipe, ct, timeout.Token))
        {
            return HandOverAnswer.NotTaken;
        }

        return await HandOverAsync(pipe, new HandedArguments { RequestId = requestId, Arguments = arguments }, ct, timeout.Token);
    }

    public void SayUnconfirmed(RunArguments arguments)
    {
        // A run that opens no window has only its standard error to say this on.
        Console.Error.WriteLine($"ILSight: the window already open never confirmed it took {string.Join(" ", CommandLines.ListArguments(arguments))}; "
            + "no second window was opened.");
    }

    private static async Task<bool> TryReachAsync(NamedPipeClientStream pipe, CancellationToken ct, CancellationToken timeout)
    {
        try
        {
            await RunArgumentsPipe.ReachAsync(pipe, timeout);
            return true;
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
    }

    private static async Task<HandOverAnswer> HandOverAsync(Stream pipe, HandedArguments handed, CancellationToken ct, CancellationToken timeout)
    {
        try
        {
            var isTaken = await RunArgumentsPipe.TryHandOverAsync(pipe, handed, timeout);
            return isTaken ? HandOverAnswer.Taken : HandOverAnswer.NotTaken;
        }
        catch (IOException)
        {
            return HandOverAnswer.Unknown;
        }
        catch (TimeoutException)
        {
            return HandOverAnswer.Unknown;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return HandOverAnswer.Unknown;
        }
        catch (InvalidDataException)
        {
            return HandOverAnswer.Unknown;
        }
    }
}
