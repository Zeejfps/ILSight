using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>One analysis of a symbol, run in the background the first time it is expanded.</summary>
public sealed class AnalyzerKindNode : AnalyzerNode
{
    public required AnalysisSymbol Subject { get; init; }

    public required AnalysisKind Kind { get; init; }

    public readonly List<AnalyzerNode> Found = new();

    public AnalyzerRun Run = new AnalyzerRun.NotStarted();

    /// <summary>What cancels the run in flight, or null while there is none.</summary>
    public CancellationTokenSource? Work => Run is AnalyzerRun.Running running ? running.Work : null;

    public bool IsDone => Run is AnalyzerRun.Done;

    public bool HasStarted => Run is not AnalyzerRun.NotStarted;
}
