namespace ILSight;

/// <summary>Stands in the results while an analysis runs, or tells why it failed.</summary>
public sealed class AnalyzerMessageNode : AnalyzerNode
{
    public string? Error { get; init; }
}
