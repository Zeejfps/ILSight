using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Analysis;
using ILSight.CommandArea;

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
                kind.CancelRun();
                CancelAll(kind.Found);
                break;
            case AnalyzerMessageNode or AnalyzerPendingNode or AnalyzerFailedNode:
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void CancelAll(IReadOnlyList<AnalyzerNode>? nodes)
    {
        if (nodes is null)
        {
            return;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            CancelNode(nodes[i]);
        }
    }

    /// <summary>Adds <paramref name="found"/>, what <paramref name="run"/> found so far, to its analysis, and notes in
    /// <paramref name="analyzer"/> that it found more; nothing once the run was stopped.</summary>
    public static void AddFound(AnalyzerTree analyzer, AnalysisRun run, IReadOnlyList<AnalysisFind> found)
    {
        if (!run.Kind.TryAddFound(run, found))
        {
            return;
        }

        analyzer.NoteFindings();
    }

    /// <summary>Ends the analysis <paramref name="run"/> filled as done, with a row telling <paramref name="error"/>
    /// when it failed, and notes it in <paramref name="analyzer"/>; nothing once the run was stopped.</summary>
    public static void FinishRun(AnalyzerTree analyzer, AnalysisRun run, string? error)
    {
        if (!run.Kind.TryFinishRun(run, error))
        {
            return;
        }

        analyzer.NoteFindings();
    }

    /// <summary>Whether analyzing <paramref name="symbol"/> in <paramref name="analyzer"/> runs, or is unavailable now
    /// and why (<see cref="AnalyzerTree.FindRefusal"/>): the decision every input of Analyze on it reads.</summary>
    public static CommandOffer<AnalysisSymbol> OfferAnalysis(AnalyzerTree analyzer, AnalysisSymbol symbol)
    {
        var refusal = analyzer.FindRefusal(symbol);
        if (refusal is not null)
        {
            return new CommandOffer<AnalysisSymbol>.Unavailable { Why = refusal };
        }

        return new CommandOffer<AnalysisSymbol>.Runs { Target = symbol };
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

    private static void ResolveAnalysis(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, AnalyzerPendingNode root, AnalysisSymbol symbol)
    {
        var shown = analyzer.ResolveRoot(root, symbol);
        if (shown is null)
        {
            return;
        }

        viewCommands.Enqueue(new ViewCommand.ShowAnalysis(shown));
    }

    /// <summary>Turns each of <paramref name="restoring"/> into the analysis of the symbol found for it, in order; one
    /// whose symbol was not found, or is in a module whose type system the library could not read, stays as saved,
    /// failed with why (<see cref="AnalyzerTree.FailRoot"/>).</summary>
    public static void AnalyzeAgain(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, IReadOnlyList<AnalyzerPendingNode> restoring,
        List<Lookup<AnalysisSymbol>> found)
    {
        for (var i = 0; i < restoring.Count; i++)
        {
            AnalyzeAgain(analyzer, viewCommands, restoring[i], found[i]);
        }
    }

    /// <summary>Keeps each of <paramref name="restoring"/> as saved, failed: looking them up failed with
    /// <paramref name="why"/>.</summary>
    public static void FailRoots(AnalyzerTree analyzer, IReadOnlyList<AnalyzerPendingNode> restoring, string why)
    {
        var failure = new RestoreFailure.Failed { Why = why };
        foreach (var root in restoring)
        {
            analyzer.FailRoot(root, failure);
        }
    }

    /// <summary>Looks again for the symbols of the restored roots of <paramref name="analyzer"/> that failed, now that
    /// the assemblies of <paramref name="assemblies"/> were read again: each waits again, as it did when it was
    /// restored, for <see cref="RestoreAnalysesHandler"/> to find. Null when none failed.</summary>
    public static AnalysesRestore? BeginRetrying(AnalyzerTree analyzer, AssemblyList assemblies)
    {
        analyzer.RetryFailedRoots();
        return BeginRestoring(analyzer, assemblies);
    }

    private static void AnalyzeAgain(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, AnalyzerPendingNode root, Lookup<AnalysisSymbol> found)
    {
        found.Switch(
            symbol => ResolveAnalysis(analyzer, viewCommands, root, symbol.Value),
            _ => analyzer.FailRoot(root, new RestoreFailure.NotFound()),
            unreadable => analyzer.FailRoot(root, new RestoreFailure.Unreadable
            {
                Name = unreadable.Module.Module.FilePath.FileName,
                Why = unreadable.Module.Why.Message,
            }));
    }
}
