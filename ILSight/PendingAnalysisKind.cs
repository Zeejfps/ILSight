using ILSight.Core.AnalysisArea;

namespace ILSight;

/// <summary>What a <see cref="PendingAnalysis"/> analyzes once its reference is found.</summary>
public union PendingAnalysisKind(PendingAnalysisKind.OfTarget, PendingAnalysisKind.OfSymbol)
{
    /// <summary>What the reference points at (<see cref="Analysis.BeginAnalyzing"/>).</summary>
    public sealed record OfTarget;

    /// <summary><see cref="Symbol"/>, whose assembly the list did not hold when it was asked for, once the reference,
    /// its own, was found and the assembly listed (<see cref="AnalyzerTree.AddRoot"/>).</summary>
    public sealed record OfSymbol
    {
        public required AnalysisSymbol Symbol { get; init; }
    }
}
