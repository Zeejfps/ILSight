using System.Diagnostics;

namespace ILSight;

/// <summary>Why two assemblies could not be compared.</summary>
public abstract record ComparisonFailure
{
    private ComparisonFailure()
    {
    }

    /// <summary>The assembly read from <see cref="File"/> is no longer open.</summary>
    public sealed record AssemblyClosed(string File) : ComparisonFailure;

    /// <summary>Comparing the assemblies <see cref="Left"/> and <see cref="Right"/> failed.</summary>
    public sealed record Failed(string Left, string Right) : ComparisonFailure;

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, AssemblyClosed, TResult> assemblyClosed,
        Func<TState, Failed, TResult> failed)
    {
        return this switch
        {
            AssemblyClosed c => assemblyClosed(state, c),
            Failed c => failed(state, c),
            _ => throw new UnreachableException($"ComparisonFailure.Match does not handle {GetType().Name}."),
        };
    }
}
