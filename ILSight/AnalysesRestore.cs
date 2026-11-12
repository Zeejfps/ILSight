using ILSight.Core;

namespace ILSight;

/// <summary>The roots restored from the saved analyses that wait for their symbols, as
/// <see cref="Analysis.BeginRestoring"/> found them, for <see cref="RestoreAnalysesHandler"/> to find: each root with
/// the analysis it was restored from, in order, and the generation of the list then, which clearing the list moves on.
/// Dropped, the roots say they are loading for good.</summary>
[MustUse]
public sealed class AnalysesRestore
{
    public required IReadOnlyList<AnalyzerPendingNode> Roots { get; init; }

    public required IReadOnlyList<CodeAnchor> Analyzed { get; init; }

    public required int Generation { get; init; }
}
