using System.Diagnostics;

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

    /// <summary>Begins a request that finds a place with <paramref name="lookup"/> in the background,
    /// resolving until it is done; the lookup's null means the place is not there. Stale when another
    /// request has begun meanwhile. <paramref name="ct"/> keeps the lookup from starting.</summary>
    public Task<Resolution<T>> ResolveAsync<T>(Func<T?> lookup, CancellationToken ct) where T : class
    {
        return RunLookupAsync(() => FoundOrMissing(lookup()), ct);
    }

    /// <inheritdoc cref="ResolveAsync{T}(Func{T}, CancellationToken)"/>
    public Task<Resolution<T>> ResolveAsync<T>(Func<T?> lookup, CancellationToken ct) where T : struct
    {
        return RunLookupAsync(() => FoundOrMissing(lookup()), ct);
    }

    private async Task<Resolution<T>> RunLookupAsync<T>(Func<Resolution<T>> lookup, CancellationToken ct)
    {
        var ticket = BeginResolving();
        var outcome = await Background.Wait(Task.Run(lookup, ct));
        return TakeResolution(ticket, outcome.Match(static ok => ok.Value, static failed => new Resolution<T>.Failed(failed.Error)));
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

/// <summary>What finding a place for a request came to: found, not there, failed, or stale because
/// the user has asked for something else since.</summary>
public abstract record Resolution<T>
{
    private Resolution()
    {
    }

    public sealed record Found(T Value) : Resolution<T>;

    /// <summary>The place is not there.</summary>
    public sealed record Missing : Resolution<T>;

    public sealed record Failed(Exception Error) : Resolution<T>;

    /// <summary>Another request began while this one was resolving: nothing is to be shown or reported.</summary>
    public sealed record Stale : Resolution<T>;

    public TResult Match<TResult>(Func<Found, TResult> found, Func<Missing, TResult> missing, Func<Failed, TResult> failed, Func<Stale, TResult> stale)
    {
        return this switch
        {
            Found f => found(f),
            Missing m => missing(m),
            Failed f => failed(f),
            Stale s => stale(s),
            _ => throw new UnreachableException($"Resolution.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Runs the handler for this case with <paramref name="state"/>, for effects that differ per
    /// case; static lambdas allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Found> found, Action<TState, Missing> missing, Action<TState, Failed> failed,
        Action<TState, Stale> stale)
    {
        switch (this)
        {
            case Found f:
                found(state, f);
                break;
            case Missing m:
                missing(state, m);
                break;
            case Failed f:
                failed(state, f);
                break;
            case Stale s:
                stale(state, s);
                break;
            default:
                throw new UnreachableException($"Resolution.Switch does not handle {GetType().Name}.");
        }
    }
}
