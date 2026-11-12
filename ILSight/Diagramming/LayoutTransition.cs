using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.Diagramming;

/// <summary>The layout a diagram is drawn in, and while a newer one comes in, the move from the one
/// before: cards glide from their old places to their new ones, and links bend from their old routes to
/// their new ones, over a fixed time. Cards and links the old layout had not appear in place.</summary>
public sealed class LayoutTransition
{
    public const float Seconds = 0.3f;

    public TypeLayout? From;
    public TypeLayout? To;
    public Dictionary<TypeLink, LinkRoute> FromRoutes = new();
    public float Elapsed = Seconds;

    /// <summary>The layout drawn, or moved to; null before the first.</summary>
    public TypeLayout? Layout => To;

    public bool IsMoving => Elapsed < Seconds;

    public float Progress => Ease.OutCubic(Elapsed / Seconds);
}
