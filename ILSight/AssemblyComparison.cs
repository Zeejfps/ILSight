using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight;

/// <summary>The two assemblies a comparison tab compares, by file, on the left and on the right, what it takes in, and
/// what the newest comparison of them that finished came to. Mutated on the UI thread only.</summary>
public sealed class AssemblyComparison
{
    private AssemblyPath? _leftPath;
    private AssemblyPath? _rightPath;

    public required AssemblyPath LeftPath
    {
        get => _leftPath ?? throw new InvalidOperationException("A comparison was made without a left assembly.");
        init => _leftPath = value;
    }

    public required AssemblyPath RightPath
    {
        get => _rightPath ?? throw new InvalidOperationException("A comparison was made without a right assembly.");
        init => _rightPath = value;
    }

    /// <summary>Changed only by <see cref="TakeInScope"/>, which counts the change.</summary>
    public ApiScope Scope { get; private set; } = ApiScope.PublicApi;

    /// <summary>What the newest comparison came to: nothing yet before the first finished, else what changed
    /// or why it failed.</summary>
    public ComparisonResult Result { get; private set; } = new ComparisonResult.NotYet();

    /// <summary>The comparison running in the background: one started cancels the one before, whose result is
    /// then dropped; once the comparison is closed, what is in flight is cancelled and its result dropped.</summary>
    public LatestWork Work { get; } = new();

    /// <summary>True while a comparison runs in the background.</summary>
    public bool IsComparing => Work.IsRunning;

    /// <summary>Counts up whenever the sides are swapped or the comparison takes in more or less; it never counts down.</summary>
    public int Version { get; private set; }

    /// <summary>Puts the right assembly on the left and the left one on the right.</summary>
    public void SwapSides()
    {
        (_leftPath, _rightPath) = (_rightPath, _leftPath);
        Version++;
    }

    /// <summary>Takes in what <paramref name="scope"/> says from now on.</summary>
    public void TakeInScope(ApiScope scope)
    {
        Scope = scope;
        Version++;
    }

    public void ShowDiff(ApiDiff diff)
    {
        Result = new ComparisonResult.Compared { Diff = diff };
    }

    /// <summary>Shows why the assemblies cannot be compared, cancelling the comparison in flight, if any.</summary>
    public void ShowFailure(ComparisonFailure failure)
    {
        Work.Cancel();
        Result = new ComparisonResult.Failed { Failure = failure };
    }
}

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
}
