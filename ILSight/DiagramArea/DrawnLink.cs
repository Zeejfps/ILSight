using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>A link as one frame draws it: the link, its route on the diagram, and its label with the label's size
/// at the diagram's own size, when it has one.</summary>
internal readonly record struct DrawnLink
{
    public required TypeLink Link { get; init; }

    public required LinkRoute Route { get; init; }

    public required string? Label { get; init; }

    public required float LabelWidth { get; init; }

    public required float LabelHeight { get; init; }
}
