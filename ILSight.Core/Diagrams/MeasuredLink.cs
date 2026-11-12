namespace ILSight.Core.Diagrams;

/// <summary>A link of a diagram with the size of its label, zero when it has none.</summary>
public readonly record struct MeasuredLink
{
    public required TypeLink Link { get; init; }

    public required float LabelWidth { get; init; }

    public required float LabelHeight { get; init; }
}
