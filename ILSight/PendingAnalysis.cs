using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>A root added to the analyzer, shown as loading, for <see cref="AnalyzeReferenceHandler"/> to turn into an
/// analysis once what <see cref="Reference"/> points at is found, its assembly listed if it was not, as following a
/// reference lists it. Dropped, the root says it is loading for good.</summary>
[MustUse]
public abstract class PendingAnalysis
{
    private PendingAnalysis()
    {
    }

    public required AnalyzerPendingNode Root { get; init; }

    public required CodeReference.Navigable Reference { get; init; }

    /// <summary>Turns <see cref="Root"/> into the analysis it waits for, now that <paramref name="found"/>, what
    /// <see cref="Reference"/> points at, was found, which <paramref name="viewCommands"/> has the view show.</summary>
    internal abstract void Analyze(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, BrowseNode found);

    /// <summary>Analyzes what <see cref="PendingAnalysis.Reference"/> points at (<see cref="Analysis.BeginAnalyzing"/>).</summary>
    public sealed class OfTarget : PendingAnalysis
    {
        internal override void Analyze(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, BrowseNode found)
        {
            Analysis.AnalyzeFound(analyzer, viewCommands, Root, found);
        }
    }

    /// <summary>Analyzes <see cref="Symbol"/>, whose assembly the list did not hold when it was asked for, once
    /// <see cref="PendingAnalysis.Reference"/>, its own, was found and the assembly listed
    /// (<see cref="AnalyzerTree.AddRoot"/>).</summary>
    public sealed class OfSymbol : PendingAnalysis
    {
        public required AnalysisSymbol Symbol { get; init; }

        internal override void Analyze(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, BrowseNode found)
        {
            Analysis.AnalyzeListed(analyzer, viewCommands, Root, Symbol);
        }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<OfTarget, TResult> ofTarget, Func<OfSymbol, TResult> ofSymbol)
    {
        return this switch
        {
            OfTarget c => ofTarget(c),
            OfSymbol c => ofSymbol(c),
            _ => throw new UnreachableException($"PendingAnalysis.Match does not handle {GetType().Name}."),
        };
    }
}
