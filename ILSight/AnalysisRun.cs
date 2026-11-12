namespace ILSight;

/// <summary>A run of one analysis <see cref="Analysis.StartRun"/> started, for <see cref="RunAnalysisHandler"/> to
/// run: the analysis it fills, and the work it runs as, which cancelling the analysis (removing its root, clearing the
/// analyzer) cancels. Dropped, the analysis says it is loading for good.</summary>
[MustUse]
public sealed class AnalysisRun
{
    public required AnalyzerKindNode Kind { get; init; }

    public required CancellationTokenSource Work { get; init; }
}
