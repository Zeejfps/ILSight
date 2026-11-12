using System.Diagnostics;

namespace ILSight;

/// <summary>Whether writing the settings is failing. A failure is reported once until a write
/// succeeds, and the autosave waits <see cref="DelaySeconds"/> before trying again, from the first
/// frame that asks after the failure.</summary>
public abstract record SaveRetry
{
    public const double DelaySeconds = 2;

    private SaveRetry()
    {
    }

    /// <summary>Writes are going through.</summary>
    public sealed record Clear : SaveRetry;

    /// <summary>A write failed, and no frame has asked since.</summary>
    public sealed record Failed : SaveRetry;

    /// <summary>A write failed, and the wait for the next try began at <see cref="Since"/>.</summary>
    public sealed record Waiting(double Since) : SaveRetry;

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Clear, TResult> clear,
        Func<TState, Failed, TResult> failed, Func<TState, Waiting, TResult> waiting)
    {
        return this switch
        {
            Clear c => clear(state, c),
            Failed c => failed(state, c),
            Waiting c => waiting(state, c),
            _ => throw new UnreachableException($"SaveRetry.Match does not handle {GetType().Name}."),
        };
    }
}
