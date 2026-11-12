using ZGF.Geometry;

namespace ILSight.Core.Diagrams;

/// <summary>Where a link runs: from its source card, or the far point of its diamond, to the base of its
/// arrowhead in <see cref="Pieces"/>, the arrowhead's tip on its target card, the center of its label when
/// it has one, and the diamond's tip on its source card when it holds its target.</summary>
public sealed record LinkRoute
{
    public required TypeLink Link { get; init; }

    public required IReadOnlyList<RoutePiece> Pieces { get; init; }

    public required PointF Tip { get; init; }

    public required PointF? LabelCenter { get; init; }

    public PointF? SourceTip { get; init; }
}
