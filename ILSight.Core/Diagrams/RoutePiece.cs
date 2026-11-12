using ZGF.Geometry;

namespace ILSight.Core.Diagrams;

/// <summary>A piece of a link's route: a cubic Bézier curve, or a straight line drawn as one.</summary>
public readonly record struct RoutePiece
{
    public required PointF Start { get; init; }

    public required PointF Control1 { get; init; }

    public required PointF Control2 { get; init; }

    public required PointF End { get; init; }
}
