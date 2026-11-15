using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What a search comes to in the assemblies it searches: a hit, or a part it couldn't read that could have
/// held one, and why: an assembly it couldn't search on, one it searched only in part, or a match it found and couldn't
/// show.</summary>
public abstract record SearchFind
{
    private SearchFind()
    {
    }

    public sealed record Hit : SearchFind
    {
        public required SearchResult Result { get; init; }
    }

    /// <summary>The library failed on <see cref="Assembly"/> while it was searched: the hits found in it before stand,
    /// and the rest of it is not searched.</summary>
    public sealed record Unsearched : SearchFind
    {
        public required LoadedModule Assembly { get; init; }

        public required string Why { get; init; }
    }

    /// <summary><see cref="Assembly"/> was searched but for <see cref="Count"/> method bodies whose IL the library
    /// couldn't read, the first for <see cref="Why"/>: what they hold is not searched.</summary>
    public sealed record PartlySearched : SearchFind
    {
        public required LoadedModule Assembly { get; init; }

        public required int Count { get; init; }

        public required string Why { get; init; }
    }

    /// <summary>A match in <see cref="Assembly"/> whose place in the tree the library couldn't read, so no node shows
    /// it: named by <see cref="Name"/> where the search read it, else by <see cref="Token"/>.</summary>
    public sealed record NotShown : SearchFind
    {
        public required LoadedModule Assembly { get; init; }

        public required int Token { get; init; }

        public required string? Name { get; init; }

        public required string Why { get; init; }
    }

    public TResult Match<TResult>(Func<Hit, TResult> hit, Func<Unsearched, TResult> unsearched, Func<PartlySearched, TResult> partlySearched,
        Func<NotShown, TResult> notShown)
    {
        return this switch
        {
            Hit h => hit(h),
            Unsearched u => unsearched(u),
            PartlySearched p => partlySearched(p),
            NotShown n => notShown(n),
            _ => throw new UnreachableException($"SearchFind.Match does not handle {GetType().Name}."),
        };
    }

    public void Switch(Action<Hit> hit, Action<Unsearched> unsearched, Action<PartlySearched> partlySearched, Action<NotShown> notShown)
    {
        switch (this)
        {
            case Hit h:
                hit(h);
                break;
            case Unsearched u:
                unsearched(u);
                break;
            case PartlySearched p:
                partlySearched(p);
                break;
            case NotShown n:
                notShown(n);
                break;
            default:
                throw new UnreachableException($"SearchFind.Switch does not handle {GetType().Name}.");
        }
    }
}
