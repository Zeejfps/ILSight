using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What work over a module's type system came to, its read awaited and matched once before the work began
/// (<see cref="LoadedAssemblies.ReadTypeSystemAsync"/>): the work's answer, or the module the library could not read,
/// with why, and no work done.</summary>
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

    public TResult Match<TResult>(Func<Done, TResult> done, Func<Unreadable, TResult> unreadable)
    {
        return this switch
        {
            Done c => done(c),
            Unreadable c => unreadable(c),
            _ => throw new UnreachableException($"TypeSystemWork.Match does not handle {GetType().Name}."),
        };
    }
}
