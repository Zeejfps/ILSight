using ILSight.Core;

namespace ILSight;

/// <summary>A root <see cref="Analysis.BeginAnalyzing"/> added, shown as loading, for
/// <see cref="AnalyzeReferenceHandler"/> to turn into the analysis of what <see cref="Reference"/> points at once it
/// is found. Dropped, the root says it is loading for good.</summary>
[MustUse]
public sealed class PendingAnalysis
{
    public required AnalyzerPendingNode Root { get; init; }

    public required CodeReference.Navigable Reference { get; init; }
}
