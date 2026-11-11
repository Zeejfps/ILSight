namespace ILSight;

/// <summary>The reload of the assemblies from disk that counts. Starting or stopping one cancels the
/// one before, whose result is then ignored.</summary>
public sealed class Reload
{
    private int _current;
    private CancellationTokenSource? _work;

    public bool IsRunning => _work is not null;

    /// <summary>Starts a reload, stopping the one before, and returns its ticket.</summary>
    public ReloadTicket Start()
    {
        Stop();
        var started = new CancellationTokenSource();
        _work = started;
        return new ReloadTicket { Id = _current, Work = started };
    }

    /// <summary>Cancels the reload under way, if any, so its result is ignored.</summary>
    public void Stop()
    {
        _current++;
        _work?.Cancel();
        _work = null;
    }

    /// <summary>Ends the reload <paramref name="ticket"/> was given for. False when another reload
    /// started or it was stopped since, and its result should be ignored.</summary>
    public bool TryFinish(ReloadTicket ticket)
    {
        ticket.Work.Dispose();
        if (ticket.Id != _current)
        {
            return false;
        }

        _work = null;
        return true;
    }
}

/// <summary>Given for one reload by <see cref="Reload.Start"/>.</summary>
public sealed class ReloadTicket
{
    public required int Id { get; init; }

    public required CancellationTokenSource Work { get; init; }

    public CancellationToken Token => Work.Token;
}
