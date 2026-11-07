using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight;

/// <summary>The two assemblies a comparison tab compares, by file, on the left and on the right, what it takes in, and
/// the newest comparison of them that finished, or why it failed. A comparison started cancels the one
/// before, whose result is then dropped; once closed, what is in flight is cancelled and its result
/// dropped. Mutated on the UI thread only.</summary>
public sealed class AssemblyComparison
{
    public readonly CancellationTokenSource Open = new();
    public CancellationTokenSource? Work;

    public required AssemblyPath LeftPath { get; set; }

    public required AssemblyPath RightPath { get; set; }

    public ApiScope Scope { get; set; } = ApiScope.PublicApi;

    /// <summary>The newest comparison that finished, or null before the first or after a failure.</summary>
    public ApiDiff? Diff { get; set; }

    /// <summary>Why the newest comparison failed, or null when none did.</summary>
    public ComparisonFailure? Failure { get; set; }

    /// <summary>True while a comparison runs in the background.</summary>
    public bool IsComparing => Work != null;

    public bool IsClosed => Open.IsCancellationRequested;

    /// <summary>Counts up whenever the sides are swapped or the comparison takes in more or less; it never counts down.</summary>
    public int Version { get; set; }
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
