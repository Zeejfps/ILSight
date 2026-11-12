using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>Stands in the results while an analysis runs, or tells why it failed, or names a module it reached that the
/// library could not read.</summary>
public sealed class AnalyzerMessageNode : AnalyzerNode
{
    public string? Error { get; init; }

    /// <summary>The module the analysis reached that the library could not read, which the row names, with why.</summary>
    public AnalysisFind.Unreadable? Unreadable { get; init; }
}
