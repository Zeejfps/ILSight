using ZGF.Geometry;

namespace ILSight.DiagramArea;

/// <summary>A card as one frame draws it: its face, the top-left of it on the diagram, whether it is
/// selected, and the row the pointer is on, or -1.</summary>
internal readonly record struct DrawnCard
{
    public required CardFace Face { get; init; }

    public required PointF Place { get; init; }

    public required bool IsSelected { get; init; }

    public required int HoveredRow { get; init; }
}
