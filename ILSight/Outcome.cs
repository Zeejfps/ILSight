using ILSight.Core;
using System.Diagnostics;

namespace ILSight;

/// <summary>What work run in the background gave: its value, or the error it failed with.</summary>
public abstract record Outcome<T>
{
    private Outcome()
    {
    }

    public sealed record Ok(T Value) : Outcome<T>;

    public sealed record Failed(Exception Error) : Outcome<T>;


    public TResult Match<TResult>(Func<Ok, TResult> ok, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Ok o => ok(o),
            Failed f => failed(f),
            _ => throw new UnreachableException($"Outcome.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it.</summary>
    public void Switch(Action<Ok> ok, Action<Failed> failed)
    {
        switch (this)
        {
            case Ok c:
                ok(c);
                return;
            case Failed c:
                failed(c);
                return;
            default:
                throw new UnreachableException($"Outcome.Switch does not handle {GetType().Name}.");
        }
    }
}

/// <summary>What work run in the background that gives nothing back came to: it ran to its end, or the error it
/// failed with. The sibling of <see cref="Outcome{T}"/>, as <see cref="Task"/> is of <see cref="Task{T}"/>.</summary>
public abstract record Outcome
{
    private Outcome()
    {
    }

    public sealed record Done : Outcome;

    public sealed record Failed(Exception Error) : Outcome;

    public TResult Match<TResult>(Func<Done, TResult> done, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Done d => done(d),
            Failed f => failed(f),
            _ => throw new UnreachableException($"Outcome.Match does not handle {GetType().Name}."),
        };
    }
}
