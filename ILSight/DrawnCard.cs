using ZGF.Geometry;

namespace ILSight;

/// <summary>A card as one frame draws it: its face, the top-left of it on the diagram, whether it is
/// selected, and the row the pointer is on, or -1.</summary>
internal readonly record struct DrawnCard(CardFace Face, PointF Place, bool IsSelected, int HoveredRow);
