using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ILSight.BuiltInArea;

namespace ILSight.DiagramArea;

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
