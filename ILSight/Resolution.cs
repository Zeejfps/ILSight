using System.Diagnostics;

namespace ILSight;

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
