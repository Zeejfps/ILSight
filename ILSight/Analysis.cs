using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>Analyzing types and members, and analyzing again what was analyzed in the last run.</summary>
public static class Analysis
{
    /// <summary>The children of <paramref name="symbol"/>, listed the first time they are asked for.</summary>
    public static List<AnalyzerNode> EnsureChildren(AnalyzerSymbolNode symbol)
    {
        if (symbol.Children is null)
        {
            symbol.Children = ListChildren(symbol);
        }

        return symbol.Children;
    }

    private static List<AnalyzerNode> ListChildren(AnalyzerSymbolNode symbol)
    {
        var children = new List<AnalyzerNode>();
        foreach (var accessor in symbol.Symbol.Accessors)
        {
            children.Add(new AnalyzerSymbolNode { Symbol = accessor });
        }

        foreach (var kind in symbol.Symbol.Kinds)
        {
            children.Add(new AnalyzerKindNode { Subject = symbol.Symbol, Kind = kind });
        }

        return children;
    }

    /// <summary>Cancels the analyses running under <paramref name="node"/>, leaving children never listed
    /// unlisted.</summary>
    internal static void CancelNode(AnalyzerNode node)
    {
        switch (node)
        {
            case AnalyzerSymbolNode symbol:
                CancelAll(symbol.Children);
                break;
            case AnalyzerKindNode kind:
                CancelRun(kind);
                CancelAll(kind.Found);
                break;
            case AnalyzerMessageNode or AnalyzerPendingNode:
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void CancelAll(List<AnalyzerNode>? nodes)
    {
        if (nodes is null)
        {
            return;
        }

        foreach (var node in nodes)
        {
            CancelNode(node);
        }
    }

    private static void CancelRun(AnalyzerKindNode kind)
    {
        if (kind.Run is not AnalyzerRun.Running running)
        {
            return;
        }

        kind.Run = new AnalyzerRun.Stopped();
        running.Work.Cancel();
    }

    /// <summary>Marks the analysis <paramref name="kind"/> stands for as running, unless it already started. Returns
    /// the run, for <see cref="RunAnalysisHandler"/> to run over the open assemblies; null when it started
    /// before.</summary>
    public static AnalysisRun? StartRun(AnalyzerKindNode kind)
    {
        if (kind.HasStarted)
        {
            return null;
        }

        var work = new CancellationTokenSource();
        kind.Run = new AnalyzerRun.Running(work);
        return new AnalysisRun { Kind = kind, Work = work };
    }

    /// <summary>Adds <paramref name="found"/>, what <paramref name="run"/> found so far, to its analysis, and notes in
    /// <paramref name="analyzer"/> that it found more; nothing once the run was stopped.</summary>
    public static void AddFound(AnalyzerTree analyzer, AnalysisRun run, IReadOnlyList<AnalysisFind> found)
    {
        var kind = run.Kind;
        if (kind.Work != run.Work)
        {
            return;
        }

        kind.Found.AddRange(CreateNodes(found));
        analyzer.NoteFindings();
    }

    /// <summary>Ends the analysis <paramref name="run"/> filled as done, with a row telling <paramref name="error"/>
    /// when it failed, and notes it in <paramref name="analyzer"/>; nothing once the run was stopped.</summary>
    public static void FinishRun(AnalyzerTree analyzer, AnalysisRun run, string? error)
    {
        var kind = run.Kind;
        if (kind.Work != run.Work)
        {
            return;
        }

        if (error is not null)
        {
            kind.Found.Add(new AnalyzerMessageNode { Error = error });
        }

        kind.Run = new AnalyzerRun.Done();
        run.Work.Dispose();
        analyzer.NoteFindings();
    }

    private static List<AnalyzerNode> CreateNodes(IReadOnlyList<AnalysisFind> found)
    {
        return found
            .Select(static f => f.Match<AnalyzerNode>(static s => new AnalyzerSymbolNode { Symbol = s.Symbol }, static u => new AnalyzerMessageNode { Unreadable = u }))
            .ToList();
    }

    /// <summary>Adds a root to <paramref name="analyzer"/> that shows as loading while what
    /// <paramref name="reference"/> points at is found. Returns it with the reference, for
    /// <see cref="AnalyzeReferenceHandler"/> to find and analyze.</summary>
    public static PendingAnalysis BeginAnalyzing(AnalyzerTree analyzer, CodeReference.Navigable reference)
    {
        return new PendingAnalysis.OfTarget { Root = analyzer.AddPending(savedAs: null), Reference = reference };
    }

    /// <summary>The roots of <paramref name="analyzer"/> restored from the saved analyses that still wait for their
    /// symbols, with the generation of <paramref name="assemblies"/> now, for <see cref="RestoreAnalysesHandler"/> to
    /// find; null when none waits.</summary>
    public static AnalysesRestore? BeginRestoring(AnalyzerTree analyzer, AssemblyList assemblies)
    {
        var pending = analyzer.ListRestoring();
        if (pending.Count == 0)
        {
            return null;
        }

        return new AnalysesRestore
        {
            Roots = pending.ConvertAll(static r => r.Root),
            Analyzed = pending.ConvertAll(static r => r.SavedAs),
            Generation = assemblies.Generation,
        };
    }

    /// <summary>Turns <paramref name="root"/> into the analysis of what <paramref name="node"/> stands for, which
    /// <paramref name="viewCommands"/> has the view show; removes it when that cannot be analyzed.</summary>
    public static void AnalyzeFound(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, AnalyzerPendingNode root, BrowseNode node)
    {
        var symbol = node.Analysis;
        if (symbol is null)
        {
            analyzer.RemoveRoot(root);
            return;
        }
        ResolveAnalysis(analyzer, viewCommands, root, symbol);
    }

    /// <summary>Turns <paramref name="root"/>, which waited while the assembly <paramref name="symbol"/> is in was
    /// listed, into the analysis of <paramref name="symbol"/>, which <paramref name="viewCommands"/> has the view show;
    /// removes it when that assembly left the list meanwhile.</summary>
    public static void AnalyzeListed(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, AnalyzerPendingNode root, AnalysisSymbol symbol)
    {
        ResolveAnalysis(analyzer, viewCommands, root, symbol);
    }

    /// <summary>Turns <paramref name="root"/> into the analysis of <paramref name="symbol"/>, and commands
    /// the view to show the root that analyzes it.</summary>
    private static void ResolveAnalysis(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, AnalyzerPendingNode root, AnalysisSymbol symbol)
    {
        var shown = analyzer.ResolveRoot(root, symbol);
        if (shown is null)
        {
            return;
        }

        viewCommands.Enqueue(new ViewCommand.ShowAnalysis(shown));
    }

    /// <summary>Turns each of <paramref name="restoring"/> into the analysis of the symbol found for it, in order, or
    /// removes it when none was, or its symbol is in a module whose type system the library could not read.</summary>
    public static void AnalyzeAgain(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, IReadOnlyList<AnalyzerPendingNode> restoring,
        List<Lookup<AnalysisSymbol>> found)
    {
        for (var i = 0; i < restoring.Count; i++)
        {
            AnalyzeAgain(analyzer, viewCommands, restoring[i], found[i]);
        }
    }

    /// <summary>Removes each of <paramref name="restoring"/> from <paramref name="analyzer"/>.</summary>
    public static void RemoveRoots(AnalyzerTree analyzer, IReadOnlyList<AnalyzerPendingNode> restoring)
    {
        foreach (var root in restoring)
        {
            analyzer.RemoveRoot(root);
        }
    }

    private static void AnalyzeAgain(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, AnalyzerPendingNode root, Lookup<AnalysisSymbol> found)
    {
        if (found is not Lookup<AnalysisSymbol>.Found symbol)
        {
            analyzer.RemoveRoot(root);
            return;
        }
        ResolveAnalysis(analyzer, viewCommands, root, symbol.Value);
    }
}
