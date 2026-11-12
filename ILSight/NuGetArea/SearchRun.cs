using System.Diagnostics;

namespace ILSight.NuGetArea;

/// <summary>The last search: none since the dialog was made or the search was dropped, one
/// running, or one that found a page or failed to.</summary>
internal abstract record SearchRun
{
    private SearchRun()
    {
    }

    public sealed record None : SearchRun;

    public sealed record Running(FeedSearch Search) : SearchRun;

    public sealed record Found(FeedSearch Search) : SearchRun;

    public sealed record Failed : SearchRun
    {
        public required FeedSearch Search { get; init; }

        public required string Error { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<None, TResult> none, Func<Running, TResult> running, Func<Found, TResult> found,
        Func<Failed, TResult> failed)
    {
        return this switch
        {
            None c => none(c),
            Running c => running(c),
            Found c => found(c),
            Failed c => failed(c),
            _ => throw new UnreachableException($"SearchRun.Match does not handle {GetType().Name}."),
        };
    }
}
