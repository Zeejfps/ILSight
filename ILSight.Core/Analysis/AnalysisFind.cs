using System.Diagnostics;

namespace ILSight.Core.Analysis;

/// <summary>What an analysis finds: a symbol, or a module it should have searched that the library could not read,
/// named beside what the others gave.</summary>
public abstract record AnalysisFind
{
    private AnalysisFind()
    {
    }

    public sealed record Found : AnalysisFind
    {
        public required AnalysisSymbol Symbol { get; init; }
    }

    public sealed record Unreadable : AnalysisFind
    {
        /// <summary>The module's file name.</summary>
        public required string Name { get; init; }

        /// <summary>Why the library could not read it.</summary>
        public required string Why { get; init; }
    }

    public TResult Match<TResult>(Func<Found, TResult> found, Func<Unreadable, TResult> unreadable)
    {
        return this switch
        {
            Found c => found(c),
            Unreadable c => unreadable(c),
            _ => throw new UnreachableException($"AnalysisFind.Match does not handle {GetType().Name}."),
        };
    }
}
