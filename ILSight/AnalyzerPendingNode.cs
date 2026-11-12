using ILSight.Core;

namespace ILSight;

/// <summary>A root whose symbol is still being found, shown as loading. It saves as the analysis it
/// was restored from, if it was.</summary>
public sealed class AnalyzerPendingNode : AnalyzerNode
{
    public required CodeAnchor? SavedAs { get; init; }
}
