namespace ILSight;

/// <summary>Given for one piece of work <see cref="LatestWork"/> started, to cancel it and to tell whether it was
/// overtaken.</summary>
public sealed class WorkTicket
{
    private readonly CancellationTokenSource _work;

    internal WorkTicket(CancellationTokenSource work)
    {
        _work = work;
        Token = work.Token;
    }

    /// <summary>Fires once the work is cancelled; still readable once the ticket is let go of.</summary>
    public CancellationToken Token { get; }

    internal void Cancel()
    {
        _work.Cancel();
    }

    internal void Release()
    {
        _work.Dispose();
    }
}
