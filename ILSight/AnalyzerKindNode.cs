using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>One analysis of a symbol, run in the background the first time it is expanded. What a run finds lands
/// only while that run is the analysis's own and still running.</summary>
public sealed class AnalyzerKindNode : AnalyzerNode
{
    private readonly List<AnalyzerNode> _found = [];

    public required AnalysisSymbol Subject { get; init; }

    public required AnalysisKind Kind { get; init; }

    public IReadOnlyList<AnalyzerNode> Found => _found;

    public AnalyzerRun Run { get; private set; } = new AnalyzerRun.NotStarted();

    /// <summary>What cancels the run in flight, or null while there is none.</summary>
    public CancellationTokenSource? Work => Run is AnalyzerRun.Running running ? running.Work : null;

    public bool IsDone => Run is AnalyzerRun.Done;

    public bool HasStarted => Run is not AnalyzerRun.NotStarted;

    /// <summary>Marks the analysis as running, unless it already started. Returns the run, for
    /// <see cref="RunAnalysisHandler"/> to run over the open assemblies; null when it started before.</summary>
    public AnalysisRun? StartRun()
    {
        if (HasStarted)
        {
            return null;
        }

        var work = new CancellationTokenSource();
        Run = new AnalyzerRun.Running(work);
        return new AnalysisRun { Kind = this, Work = work };
    }

    /// <summary>Stops the run in flight and cancels its work; nothing when none runs.</summary>
    public void CancelRun()
    {
        if (Run is not AnalyzerRun.Running running)
        {
            return;
        }

        Run = new AnalyzerRun.Stopped();
        running.Work.Cancel();
    }

    /// <summary>Adds <paramref name="found"/>, what <paramref name="run"/> found so far; false, adding nothing, once
    /// the run was stopped.</summary>
    public bool TryAddFound(AnalysisRun run, IReadOnlyList<AnalysisFind> found)
    {
        if (Work != run.Work)
        {
            return false;
        }

        _found.AddRange(CreateNodes(found));
        return true;
    }

    /// <summary>Ends <paramref name="run"/> as done, with a row telling <paramref name="error"/> when it failed, and
    /// disposes its work; false, doing nothing, once the run was stopped.</summary>
    public bool TryFinishRun(AnalysisRun run, string? error)
    {
        if (Work != run.Work)
        {
            return false;
        }

        if (error is not null)
        {
            _found.Add(new AnalyzerMessageNode { Error = error });
        }

        Run = new AnalyzerRun.Done();
        run.Work.Dispose();
        return true;
    }

    private static List<AnalyzerNode> CreateNodes(IReadOnlyList<AnalysisFind> found)
    {
        return found
            .Select(static f => f.Match<AnalyzerNode>(static s => new AnalyzerSymbolNode { Symbol = s.Symbol }, static u => new AnalyzerMessageNode { Unreadable = u }))
            .ToList();
    }
}
