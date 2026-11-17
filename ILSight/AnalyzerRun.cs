namespace ILSight;

/// <summary>How one analysis of a symbol stands. It runs once: stopped is cancelled, and it doesn't run
/// again.</summary>
public union AnalyzerRun(AnalyzerRun.NotStarted, AnalyzerRun.Running, AnalyzerRun.Stopped, AnalyzerRun.Done)
{
    public sealed record NotStarted;

    /// <summary>In flight; <see cref="Work"/> cancels it.</summary>
    public sealed record Running(CancellationTokenSource Work);

    public sealed record Stopped;

    public sealed record Done;
}
