using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What work over a module's type system came to, its read awaited and matched once before the work began
/// (<see cref="LoadedAssemblies.ReadTypeSystemAsync"/>): the work's answer, or the module the library could not read,
/// with why, and no work done, or the module released before the work began (closed), and nothing done.</summary>
public abstract record TypeSystemWork<T>
{
    private TypeSystemWork()
    {
    }

    public sealed record Done : TypeSystemWork<T>
    {
        public required T Value { get; init; }
    }

    public sealed record Unreadable : TypeSystemWork<T>
    {
        public required UnreadableModule Module { get; init; }
    }

    public sealed record Closed : TypeSystemWork<T>
    {
        public required LoadedModule Module { get; init; }
    }

    public TResult Match<TResult>(Func<Done, TResult> done, Func<Unreadable, TResult> unreadable, Func<Closed, TResult> closed)
    {
        return this switch
        {
            Done c => done(c),
            Unreadable c => unreadable(c),
            Closed c => closed(c),
            _ => throw new UnreachableException($"TypeSystemWork.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it.</summary>
    public void Switch(Action<Done> done, Action<Unreadable> unreadable, Action<Closed> closed)
    {
        switch (this)
        {
            case Done c:
                done(c);
                return;
            case Unreadable c:
                unreadable(c);
                return;
            case Closed c:
                closed(c);
                return;
            default:
                throw new UnreachableException($"TypeSystemWork.Switch does not handle {GetType().Name}.");
        }
    }
}
