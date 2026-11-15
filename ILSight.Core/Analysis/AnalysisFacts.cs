namespace ILSight.Core.Analysis;

/// <summary>What a tree node's analyses are decided on: the facts of the type or member it stands for, read out of the
/// library as the node was made, inside its parent's listing. The app's table of analyses is applied to them only when
/// they are first asked for (<see cref="Symbol"/>), outside that listing's catch, so a bug in the table fails the ask
/// at its edge and never the parent's children as unreadable.</summary>
public sealed class AnalysisFacts
{
    private readonly Lazy<AnalysisSymbol> symbol;

    internal AnalysisFacts(SymbolFacts facts)
    {
        Facts = facts;
        symbol = new Lazy<AnalysisSymbol>(() => Analyzers.Compose(this));
    }

    internal SymbolFacts Facts { get; }

    /// <summary>The symbol, with the analyses the app's table offers for it, composed the first time it is asked for and
    /// the same object after; what the table throws is thrown to the one asking, each time.</summary>
    public AnalysisSymbol Symbol => symbol.Value;
}
