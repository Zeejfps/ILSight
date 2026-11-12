namespace ILSight;

/// <summary>What the user last asked for. Each request takes a ticket; background work started for
/// an older ticket is stale once the user asks for something else.</summary>
public sealed class UserIntent
{
    private const int NoTicket = -1;

    private int _current;
    private int _resolving = NoTicket;

    /// <summary>True while the current request is finding a place in the background.</summary>
    public bool IsResolving => _resolving == _current;

    public int Begin()
    {
        _current++;
        return _current;
    }

    public bool IsCurrent(int ticket)
    {
        return ticket == _current;
    }

    public bool IsStale(int ticket)
    {
        return ticket != _current;
    }

    public void StartResolving(int ticket)
    {
        _resolving = ticket;
    }

    public void StopResolving()
    {
        _resolving = NoTicket;
    }

    /// <summary>Begins a request that finds a place in the background: it is resolving until
    /// <see cref="EndResolving{T}(int, Outcome{T})"/> takes what was found for the ticket this returns.</summary>
    public int BeginResolving()
    {
        var ticket = Begin();
        StartResolving(ticket);
        return ticket;
    }

    /// <summary>What finding a place for <paramref name="ticket"/>, begun by <see cref="BeginResolving"/>, came to:
    /// the lookup's <paramref name="outcome"/>, whose null means the place is not there; stale, leaving the request
    /// begun since alone, when another one has begun meanwhile. Stops resolving unless stale.</summary>
    public Resolution<T> EndResolving<T>(int ticket, Outcome<T?> outcome) where T : class
    {
        return TakeResolution(ticket, outcome.Match(static ok => FoundOrMissing(ok.Value), static failed => new Resolution<T>.Failed(failed.Error)));
    }

    /// <inheritdoc cref="EndResolving{T}(int, Outcome{T})"/>
    public Resolution<T> EndResolving<T>(int ticket, Outcome<T?> outcome) where T : struct
    {
        return TakeResolution(ticket, outcome.Match(static ok => FoundOrMissing(ok.Value), static failed => new Resolution<T>.Failed(failed.Error)));
    }

    /// <summary><paramref name="resolution"/>, unless another request began since <paramref name="ticket"/>: then
    /// stale, the request begun since left resolving as it is.</summary>
    private Resolution<T> TakeResolution<T>(int ticket, Resolution<T> resolution)
    {
        if (IsStale(ticket))
        {
            return new Resolution<T>.Stale();
        }

        StopResolving();
        return resolution;
    }

    private static Resolution<T> FoundOrMissing<T>(T? value) where T : class
    {
        if (value is null)
        {
            return new Resolution<T>.Missing();
        }

        return new Resolution<T>.Found(value);
    }

    private static Resolution<T> FoundOrMissing<T>(T? value) where T : struct
    {
        if (value is null)
        {
            return new Resolution<T>.Missing();
        }

        return new Resolution<T>.Found(value.Value);
    }
}
