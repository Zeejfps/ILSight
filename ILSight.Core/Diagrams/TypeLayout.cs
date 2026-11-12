using ZGF.Geometry;

namespace ILSight.Core.Diagrams;

/// <summary>Where a diagram's cards go and its links run, in layers with the types derived from and held
/// above those deriving from and holding them, as MSAGL's layered layout places them. Coordinates run
/// right and down from the top-left of the whole layout, which is <see cref="Width"/> by
/// <see cref="Height"/>.</summary>
public sealed class TypeLayout
{
    /// <summary>How long the arrowhead at the end of a route is.</summary>
    public const float ArrowLength = 12f;

    /// <summary>How long the diamond at the start of a route to a type held is.</summary>
    public const float DiamondLength = 16f;

    /// <summary>The top-left corner of each card.</summary>
    public required IReadOnlyDictionary<CodeReference.Entity, PointF> Places { get; init; }

    public required IReadOnlyList<LinkRoute> Routes { get; init; }

    public required float Width { get; init; }

    public required float Height { get; init; }
}
