namespace ILSight.Core.Diagrams;

/// <summary>What a diagram is laid out from: the size of each card and each link's label.</summary>
public sealed record DiagramMeasures
{
    public required IReadOnlyList<MeasuredCard> Cards { get; init; }

    public required IReadOnlyList<MeasuredLink> Links { get; init; }
}
