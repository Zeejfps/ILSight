using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What a unit reading one module came to: what it read, or that the module was released (closed, and
/// nothing read it any more) before the unit's reading began, and nothing was read
/// (<see cref="LoadedAssemblies.TryRead"/>).</summary>
public abstract record ModuleRead<T>
{
    private ModuleRead()
    {
    }

    public sealed record Read : ModuleRead<T>
    {
        public required T Value { get; init; }
    }

    public sealed record Closed : ModuleRead<T>
    {
        public required LoadedModule Module { get; init; }
    }

    public TResult Match<TResult>(Func<Read, TResult> read, Func<Closed, TResult> closed)
    {
        return this switch
        {
            Read r => read(r),
            Closed c => closed(c),
            _ => throw new UnreachableException($"ModuleRead.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it.</summary>
    public void Switch(Action<Read> read, Action<Closed> closed)
    {
        switch (this)
        {
            case Read r:
                read(r);
                return;
            case Closed c:
                closed(c);
                return;
            default:
                throw new UnreachableException($"ModuleRead.Switch does not handle {GetType().Name}.");
        }
    }
}
