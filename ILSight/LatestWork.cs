namespace ILSight;

/// <summary>The newest of a series of background works for one owner, whose result is the only one that counts:
/// starting one cancels the one before, whose result is then dropped. Once closed, what is in flight is
/// cancelled and its result dropped, and work started later is cancelled from the start. Used on the UI
/// thread only.</summary>
public sealed class LatestWork
{
    private readonly CancellationTokenSource _open = new();
    private WorkTicket? _current;

    /// <summary>True from <see cref="Start"/> until its work is finished, cancelled or overtaken.</summary>
    public bool IsRunning => _current is not null;

    public bool IsClosed => _open.IsCancellationRequested;

    /// <summary>Cancelled once closed.</summary>
    public CancellationToken UntilClosed => _open.Token;

    /// <summary>Starts work, cancelling the work in flight, whose result is then dropped. Once closed, the
    /// work is cancelled from the start and its result dropped too. The edge that starts the work links the
    /// ticket's token to the workflow's, so that being overtaken or closed is a cancellation asked for.</summary>
    public WorkTicket Start()
    {
        Cancel();
        var ticket = new WorkTicket(CancellationTokenSource.CreateLinkedTokenSource(_open.Token));
        if (!IsClosed)
        {
            _current = ticket;
        }

        return ticket;
    }

    /// <summary>Ends the work <paramref name="ticket"/> was given for. False when other work started since, or
    /// it was cancelled or closed, and its result should be dropped.</summary>
    public bool TryFinish(WorkTicket ticket)
    {
        ticket.Release();
        if (_current != ticket)
        {
            return false;
        }

        _current = null;
        return true;
    }

    /// <summary>Cancels the work in flight, if any, whose result is then dropped. Its ticket is let go of at
    /// once, as work that ends cancelled never gets to <see cref="TryFinish"/>.</summary>
    public void Cancel()
    {
        var current = _current;
        if (current is null)
        {
            return;
        }

        _current = null;
        current.Cancel();
        current.Release();
    }

    /// <summary>Cancels the work in flight for good: its result is dropped, and so is that of work started later.</summary>
    public void Close()
    {
        _open.Cancel();
        _current = null;
    }
}
