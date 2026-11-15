using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>Stands in the results while an analysis runs, or tells why it failed, or names a module it reached that the
/// library could not read, or one it read only in part.</summary>
public sealed class AnalyzerMessageNode : AnalyzerNode
{
    public string? Error { get; init; }

    /// <summary>The module the analysis reached that the library could not read, which the row names, with why.</summary>
    public AnalysisFind.Unreadable? Unreadable { get; init; }

    /// <summary>The module the analysis read only in part, which the row names, with how many parts and why.</summary>
    public AnalysisFind.PartlyRead? PartlyRead { get; init; }
}
