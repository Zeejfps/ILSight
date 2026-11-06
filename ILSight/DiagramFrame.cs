using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ILSight.Core.Diagrams;

namespace ILSight;

/// <summary>A card as one frame draws it: its face, the top-left of it on the diagram, whether it is
/// selected, and the row the pointer is on, or -1.</summary>
internal readonly record struct DrawnCard(CardFace Face, PointF Place, bool IsSelected, int HoveredRow);

/// <summary>A link as one frame draws it: how it relates its types, its route on the diagram, and its label
/// with the label's size at the diagram's own size, when it has one.</summary>
internal readonly record struct DrawnLink(TypeLinkKind Kind, LinkRoute Route, string? Label, float LabelWidth, float LabelHeight);

/// <summary>What one frame of a diagram is painted from, refilled each frame: the links, then the cards
/// over them, in the diagram's coordinates, drawn from <see cref="Corner"/> at <see cref="Zoom"/>.</summary>
internal sealed class DiagramFrame
{
    public readonly List<DrawnLink> Links = new();
    public readonly List<DrawnCard> Cards = new();
    public PointF Corner;
    public float Zoom = 1f;
    public UiTheme Theme = UiTheme.Dark;
    public NodeIconPalette Palette = BuiltInThemes.Dark.Icons;
    public AppIcons Icons = BuiltInIconThemes.Reference.Icons;
}
