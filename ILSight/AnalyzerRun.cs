namespace ILSight;

/// <summary>How one analysis of a symbol stands. It runs once: stopped is cancelled, and it doesn't run
/// again.</summary>
public abstract record AnalyzerRun
{
    private AnalyzerRun()
    {
    }

    public sealed record NotStarted : AnalyzerRun;

    /// <summary>In flight; <see cref="Work"/> cancels it.</summary>
    public sealed record Running(CancellationTokenSource Work) : AnalyzerRun;

    public sealed record Stopped : AnalyzerRun;

    public sealed record Done : AnalyzerRun;
}
