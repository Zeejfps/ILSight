using ILSight.Core;
using ILSight.Core.AnalysisArea;

namespace ILSight;

/// <summary>A root added to the analyzer, shown as loading, for <see cref="AnalyzeReferenceHandler"/> to turn into an
/// analysis once what <see cref="Reference"/> points at is found, its assembly listed if it was not, as following a
/// reference lists it. Dropped, the root says it is loading for good.</summary>
[MustUse]
public sealed class PendingAnalysis
{
    public required AnalyzerPendingNode Root { get; init; }

    public required CodeReference.Navigable Reference { get; init; }

    /// <summary>What is analyzed once <see cref="Reference"/> is found: what it points at, or a symbol of its own.</summary>
    public required PendingAnalysisKind Kind { get; init; }

    /// <summary>Turns <see cref="Root"/> into the analysis it waits for, now that <paramref name="found"/>, what
    /// <see cref="Reference"/> points at, was found, which <paramref name="viewCommands"/> has the view show.</summary>
    internal void Analyze(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, BrowseNode found)
    {
        switch (Kind)
        {
            case PendingAnalysisKind.OfTarget:
                Analysis.AnalyzeFound(analyzer, viewCommands, Root, found);
                break;
            case PendingAnalysisKind.OfSymbol ofSymbol:
                Analysis.AnalyzeListed(analyzer, viewCommands, Root, ofSymbol.Symbol);
                break;
        }
    }
}
