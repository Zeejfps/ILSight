using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What a search comes to in the assemblies it searches: a hit, or an assembly it couldn't search, and
/// why.</summary>
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

    public TResult Match<TResult>(Func<Hit, TResult> hit, Func<Unsearched, TResult> unsearched)
    {
        return this switch
        {
            Hit h => hit(h),
            Unsearched u => unsearched(u),
            _ => throw new UnreachableException($"SearchFind.Match does not handle {GetType().Name}."),
        };
    }

    public void Switch(Action<Hit> hit, Action<Unsearched> unsearched)
    {
        switch (this)
        {
            case Hit h:
                hit(h);
                break;
            case Unsearched u:
                unsearched(u);
                break;
            default:
                throw new UnreachableException($"SearchFind.Switch does not handle {GetType().Name}.");
        }
    }
}
