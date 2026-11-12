using ZGF.Geometry;

namespace ILSight.Core.Diagrams;

/// <summary>A piece of a link's route: a cubic Bézier curve, or a straight line drawn as one.</summary>
public readonly record struct RoutePiece(PointF Start, PointF Control1, PointF Control2, PointF End);
