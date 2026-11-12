using System.Diagnostics;
using ILSight.Core.Comparison;

namespace ILSight;

/// <summary>What the newest comparison of two assemblies came to.</summary>
public abstract record ComparisonResult
{
    private ComparisonResult()
    {
    }

    /// <summary>No comparison has finished yet.</summary>
    public sealed record NotYet : ComparisonResult;

    /// <summary>The comparison finished, finding <see cref="Diff"/>.</summary>
    public sealed record Compared : ComparisonResult
    {
        public required ApiDiff Diff { get; init; }
    }

    /// <summary>The comparison could not be made, for <see cref="Failure"/>.</summary>
    public sealed record Failed : ComparisonResult
    {
        public required ComparisonFailure Failure { get; init; }
    }

    public TResult Match<TResult>(Func<NotYet, TResult> notYet, Func<Compared, TResult> compared, Func<Failed, TResult> failed)
    {
        return this switch
        {
            NotYet n => notYet(n),
            Compared c => compared(c),
            Failed f => failed(f),
            _ => throw new UnreachableException($"ComparisonResult.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary><see cref="Match{TResult}"/> handing <paramref name="state"/> to the handlers, so that a
    /// caller drawing every frame can pass static lambdas and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, NotYet, TResult> notYet, Func<TState, Compared, TResult> compared,
        Func<TState, Failed, TResult> failed)
    {
        return this switch
        {
            NotYet n => notYet(state, n),
            Compared c => compared(state, c),
            Failed f => failed(state, f),
            _ => throw new UnreachableException($"ComparisonResult.Match does not handle {GetType().Name}."),
        };
    }
}
