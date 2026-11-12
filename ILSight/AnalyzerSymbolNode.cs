using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>A symbol analyzed or found. Its children are its accessors, if it is a property or
/// event, then each analysis that applies to it.</summary>
public sealed class AnalyzerSymbolNode : AnalyzerNode
{
    public required AnalysisSymbol Symbol { get; init; }

    /// <summary>The children, listed by <see cref="Analysis.EnsureChildren"/> the first time they are asked for.</summary>
    public List<AnalyzerNode>? Children;
}
