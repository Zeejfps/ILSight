namespace ILSight;

/// <summary>What asking the analyzer to analyze a symbol came to (<see cref="AnalyzerTree.AddRoot"/>): the root
/// analyzing it; a root waiting while its assembly, which the list neither held nor had on its way, is listed, for the
/// edge to start; or refused, with why.</summary>
[MustUse]
public union AnalyzerAdd(AnalyzerAdd.Analyzing, AnalyzerAdd.Listing, AnalyzerAdd.Refused)
{
    /// <summary>The root analyzing the symbol: added, or the one analyzing it already.</summary>
    public sealed record Analyzing
    {
        public required AnalyzerSymbolNode Root { get; init; }
    }

    /// <summary>A root waiting, shown as loading, while the symbol's assembly is listed as following a reference into
    /// it lists it, the analysis landing then: for the edge to start (<see cref="AnalyzeReferenceHandler"/>).</summary>
    public sealed record Listing
    {
        public required PendingAnalysis Pending { get; init; }
    }

    /// <summary>Nothing was added: the symbol's assembly isn't on the list and can't be listed, as
    /// <see cref="Why"/> tells.</summary>
    public sealed record Refused
    {
        public required Refusal Why { get; init; }
    }
}
