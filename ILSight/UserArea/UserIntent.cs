using ILSight.Core;

namespace ILSight.UserArea;

/// <summary>What the user last asked for. Each request takes a ticket; background work started for
/// an older ticket is stale once the user asks for something else.</summary>
public sealed class UserIntent
{
    private const int NoTicket = -1;

    private int _current;
    private int _resolving = NoTicket;
    private int _opening = NoTicket;

    /// <summary>True while the current request is finding a place in the background.</summary>
    public bool IsResolving => _resolving == _current;

    /// <summary>True while the current request is opening the files it named, until what they opened is taken
    /// (<see cref="EndOpening"/>).</summary>
    public bool IsOpening => _opening == _current;

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

    /// <summary>Begins a request to open <paramref name="paths"/>: what was still being looked up for an older request
    /// is dropped. Returns the files with the request's ticket; null, beginning nothing, when there are none.</summary>
    public FileOpening? BeginOpening(IEnumerable<AssemblyPath> paths)
    {
        var files = paths.ToArray();
        if (files.Length == 0)
        {
            return null;
        }

        var ticket = Begin();
        _opening = ticket;
        return new FileOpening { Paths = files, Intent = ticket };
    }

    /// <summary>Takes what opening the files of <paramref name="ticket"/> came to: the request is no longer opening,
    /// unless another one has begun since.</summary>
    public void EndOpening(int ticket)
    {
        if (ticket != _opening)
        {
            return;
        }

        _opening = NoTicket;
    }

    /// <summary>Begins a request that finds a place in the background: it is resolving until
    /// <see cref="EndResolving{T}(int, Outcome{Lookup{T}})"/> takes what was found for the ticket this returns.</summary>
    public int BeginResolving()
    {
        var ticket = Begin();
        StartResolving(ticket);
        return ticket;
    }

    /// <summary>What finding a place for <paramref name="ticket"/>, begun by <see cref="BeginResolving"/>, came to:
    /// the lookup in <paramref name="outcome"/>, found, not found, or unreadable; stale, leaving the request begun since
    /// alone, when another one has begun meanwhile. Stops resolving unless stale.</summary>
    public Resolution<T> EndResolving<T>(int ticket, Outcome<Lookup<T>> outcome) where T : notnull
    {
        return TakeResolution(ticket, outcome.Match(static ok => Resolve(ok.Value), static failed => new Resolution<T>.Failed(failed.Error)));
    }

    private Resolution<T> TakeResolution<T>(int ticket, Resolution<T> resolution)
    {
        if (IsStale(ticket))
        {
            // The request begun since is left resolving as it is.
            return new Resolution<T>.Stale();
        }

        StopResolving();
        return resolution;
    }

    private static Resolution<T> Resolve<T>(Lookup<T> lookup) where T : notnull
    {
        return lookup.Match<Resolution<T>>(
            static found => new Resolution<T>.Found(found.Value),
            static _ => new Resolution<T>.Missing(),
            static unreadable => new Resolution<T>.Unreadable { Module = unreadable.Module });
    }
}
