namespace ILSight.Core.Diagrams;

/// <summary>A card of a diagram as wide and tall as it is drawn.</summary>
public readonly record struct MeasuredCard
{
    public required CodeReference.Entity Type { get; init; }

    public required float Width { get; init; }

    public required float Height { get; init; }
}
