using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>A row of the analyzer: a symbol, an analysis of one, or a placeholder.</summary>
public abstract class AnalyzerNode
{
    internal virtual void Cancel()
    {
    }
}

/// <summary>A symbol analyzed or found. Its children are its accessors, if it is a property or
/// event, then each analysis that applies to it.</summary>
public sealed class AnalyzerSymbolNode(AnalysisSymbol symbol) : AnalyzerNode
{
    private List<AnalyzerNode>? _children;

    public AnalysisSymbol Symbol { get; } = symbol;

    internal IReadOnlyList<AnalyzerNode> Children
    {
        get
        {
            if (_children == null)
                _children = ListChildren();
            return _children;
        }
    }

    private List<AnalyzerNode> ListChildren()
    {
        var children = new List<AnalyzerNode>();
        foreach (var accessor in Analyzers.AccessorsOf(Symbol))
            children.Add(new AnalyzerSymbolNode(accessor));
        foreach (var kind in Analyzers.KindsFor(Symbol))
            children.Add(new AnalyzerKindNode(Symbol, kind));
        return children;
    }

    /// <summary>Cancels the analyses running under it, leaving children never listed unlisted.</summary>
    internal override void Cancel()
    {
        if (_children == null)
            return;
        foreach (var child in _children)
            child.Cancel();
    }
}

/// <summary>One analysis of a symbol, run in the background the first time it is expanded.</summary>
public sealed class AnalyzerKindNode(AnalysisSymbol subject, AnalysisKind kind) : AnalyzerNode
{
    private readonly List<AnalyzerNode> _found = new();
    private CancellationTokenSource? _work;
    public AnalysisSymbol Subject { get; } = subject;
    public AnalysisKind Kind { get; } = kind;
    public bool IsDone { get; private set; }
    public bool HasStarted { get; private set; }
    public IReadOnlyList<AnalyzerNode> Found => _found;

    /// <summary>Runs the analysis over <paramref name="assemblies"/> unless it already started,
    /// calling <paramref name="changed"/> each time results come in. When it fails, it ends with a row
    /// telling why and hands the exception to <paramref name="failed"/>.</summary>
    internal void StartOnce(AssemblyList assemblies, Action changed, Action<Exception> failed, Action<AppProblem> reportError)
    {
        if (HasStarted)
            return;
        HasStarted = true;
        Background.Start(RunAsync(assemblies, changed, failed), reportError);
    }

    private async Task RunAsync(AssemblyList assemblies, Action changed, Action<Exception> failed)
    {
        var work = new CancellationTokenSource();
        _work = work;
        var token = work.Token;
        try
        {
            await foreach (var batch in Analyzers.RunAsync(assemblies, Subject, Kind, token))
                Deliver(work, CreateNodes(batch), changed);
            Finish(work, [], changed);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Finish(work, [new AnalyzerMessageNode(e.Message)], changed);
            failed(e);
        }
    }

    private static List<AnalyzerNode> CreateNodes(IReadOnlyList<AnalysisSymbol> symbols)
    {
        return symbols
            .Select(static s => (AnalyzerNode)new AnalyzerSymbolNode(s))
            .ToList();
    }

    private void Deliver(CancellationTokenSource work, List<AnalyzerNode> found, Action changed)
    {
        if (_work != work)
            return;
        _found.AddRange(found);
        changed();
    }

    private void Finish(CancellationTokenSource work, List<AnalyzerNode> found, Action changed)
    {
        if (_work != work)
            return;
        _found.AddRange(found);
        IsDone = true;
        _work = null;
        work.Dispose();
        changed();
    }

    internal override void Cancel()
    {
        CancelRun();
        foreach (var child in _found)
            child.Cancel();
    }

    private void CancelRun()
    {
        var work = _work;
        if (work == null)
            return;
        _work = null;
        work.Cancel();
    }
}

/// <summary>Stands in the results while an analysis runs, or tells why it failed.</summary>
public sealed class AnalyzerMessageNode(string? error = null) : AnalyzerNode
{
    public string? Error { get; } = error;
}

/// <summary>A root whose symbol is still being found, shown as loading. It saves as the analysis it
/// was restored from, if it was.</summary>
public sealed class AnalyzerPendingNode(CodeAnchor? savedAs) : AnalyzerNode
{
    public CodeAnchor? SavedAs { get; } = savedAs;
}

/// <summary>
/// The analyzer's tree: the symbols the user asked to analyze, newest last, each standing as a
/// <see cref="AnalyzerPendingNode"/> until it is found, with the analyses under them. Results stream
/// in from the background and keep the order the analysis finds them in, or ILSpy's sorting when it
/// asks for one.
/// </summary>
public sealed class AnalyzerTree
{
    public readonly List<AnalyzerNode> Roots = new();

    /// <summary>Changes whenever what <see cref="Analysis.ListSavedAnalyses"/> lists changes.</summary>
    public int Revision;

    /// <summary>Changes whenever a root is added, replaced or removed.</summary>
    public int Changes;

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
