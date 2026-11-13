using System.Diagnostics;

namespace ILSight.CommandArea;

/// <summary>One decision of what a command applies to where the user is, which every input of it reads, its menu item
/// and its shortcut alike: it runs on <see cref="Runs.Target"/>; it is unavailable now, with why (the menu shows it
/// disabled with the reason, a key press reports it where the user acted); or it isn't offered there at all (the menu
/// doesn't list it, the key does nothing).</summary>
public abstract record CommandOffer<T> where T : notnull
{
    private CommandOffer()
    {
    }

    public sealed record Runs : CommandOffer<T>
    {
        public required T Target { get; init; }
    }

    public sealed record Unavailable : CommandOffer<T>
    {
        public required Refusal Why { get; init; }
    }

    public sealed record NotOffered : CommandOffer<T>;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Runs, TResult> runs, Func<Unavailable, TResult> unavailable, Func<NotOffered, TResult> notOffered)
    {
        return this switch
        {
            Runs c => runs(c),
            Unavailable c => unavailable(c),
            NotOffered c => notOffered(c),
            _ => throw new UnreachableException($"CommandOffer.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Runs the handler of whichever case this is.</summary>
    public void Switch(Action<Runs> runs, Action<Unavailable> unavailable, Action<NotOffered> notOffered)
    {
        switch (this)
        {
            case Runs c:
                runs(c);
                break;
            case Unavailable c:
                unavailable(c);
                break;
            case NotOffered c:
                notOffered(c);
                break;
            default:
                throw new UnreachableException($"CommandOffer.Switch does not handle {GetType().Name}.");
        }
    }
}
