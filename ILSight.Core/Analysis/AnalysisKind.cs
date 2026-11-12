namespace ILSight.Core.Analysis;

/// <summary>One way of relating a symbol to others, such as "Used By", as ILSpy's analyzer
/// offers it: its name and its place among the others.</summary>
public sealed class AnalysisKind
{
    public required AnalysisName Name { get; init; }

    public required int Order { get; init; }

    public required Analyzer Prepare { get; init; }

    /// <summary>Whether its results read best sorted by label, so come only once all are found.</summary>
    internal bool SortsResults { get; init; }
}
