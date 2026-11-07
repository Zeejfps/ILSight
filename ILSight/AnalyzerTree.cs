using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>A row of the analyzer: a symbol, an analysis of one, or a placeholder. The private protected
/// constructor closes it to the four cases below.</summary>
public abstract class AnalyzerNode
{
    private protected AnalyzerNode()
    {
    }
}

/// <summary>A symbol analyzed or found. Its children are its accessors, if it is a property or
/// event, then each analysis that applies to it.</summary>
public sealed class AnalyzerSymbolNode : AnalyzerNode
{
    public required AnalysisSymbol Symbol { get; init; }

    /// <summary>The children, listed by <see cref="Analysis.EnsureChildren"/> the first time they are asked for.</summary>
    public List<AnalyzerNode>? Children;
}

/// <summary>One analysis of a symbol, run in the background the first time it is expanded.</summary>
public sealed class AnalyzerKindNode : AnalyzerNode
{
    public required AnalysisSymbol Subject { get; init; }

    public required AnalysisKind Kind { get; init; }

    public readonly List<AnalyzerNode> Found = new();

    /// <summary>What cancels the run in flight, or null while there is none.</summary>
    public CancellationTokenSource? Work;

    public bool IsDone;

    public bool HasStarted;
}

/// <summary>Stands in the results while an analysis runs, or tells why it failed.</summary>
public sealed class AnalyzerMessageNode : AnalyzerNode
{
    public string? Error { get; init; }
}

/// <summary>A root whose symbol is still being found, shown as loading. It saves as the analysis it
/// was restored from, if it was.</summary>
public sealed class AnalyzerPendingNode : AnalyzerNode
{
    public required CodeAnchor? SavedAs { get; init; }
}

/// <summary>
/// The analyzer's tree: the symbols the user asked to analyze, newest last, each standing as a
/// <see cref="AnalyzerPendingNode"/> until it is found, with the analyses under them. Results stream
/// in from the background and keep the order the analysis finds them in, or ILSpy's sorting when it
/// asks for one.
/// </summary>
public sealed class AnalyzerTree
{
    public readonly TrackedList<AnalyzerNode> Roots = new();

    /// <summary>Changes whenever what <see cref="Analysis.ListSavedAnalyses"/> lists changes.</summary>
    public int Revision;

    /// <summary>Changes whenever a root is added, replaced or removed.</summary>
    public int Changes => Roots.Revision;

    /// <summary>Changes whenever an analysis under a root finds more or ends.</summary>
    public int Findings;

    /// <summary>True while a root waits for its symbol to be found.</summary>
    public bool IsFinding
    {
        get
        {
            foreach (var root in Roots)
            {
                if (root is AnalyzerPendingNode)
                    return true;
            }
            return false;
        }
    }
}
