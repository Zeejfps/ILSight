using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ILSight.BuiltInArea;

namespace ILSight.DiagramArea;

/// <summary>What one frame of a diagram is painted from, refilled each frame: the links, then the cards
/// over them, in the diagram's coordinates, drawn from <see cref="Corner"/> at <see cref="Zoom"/>. One object a
/// view keeps and lays out again each frame, so that a frame allocates nothing for it.</summary>
internal sealed class DiagramFrame
{
    private readonly List<DrawnLink> _links = new();
    private readonly List<DrawnCard> _cards = new();

    public IReadOnlyList<DrawnLink> Links => _links;

    public IReadOnlyList<DrawnCard> Cards => _cards;

    public PointF Corner { get; private set; }

    public float Zoom { get; private set; } = 1f;

    public UiTheme Theme { get; private set; } = UiTheme.Dark;

    public NodeIconPalette Palette { get; private set; } = BuiltInThemes.Dark.Icons;

    public AppIcons Icons { get; private set; } = BuiltInIconThemes.Reference.Icons;

    /// <summary>Starts the frame over, empty, drawn from <paramref name="corner"/> at <paramref name="zoom"/> in
    /// <paramref name="theme"/>, <paramref name="palette"/> and <paramref name="icons"/>.</summary>
    public void Lay(PointF corner, float zoom, UiTheme theme, NodeIconPalette palette, AppIcons icons)
    {
        _links.Clear();
        _cards.Clear();
        Corner = corner;
        Zoom = zoom;
        Theme = theme;
        Palette = palette;
        Icons = icons;
    }

    public void AddLink(DrawnLink link)
    {
        _links.Add(link);
    }

    public void AddCard(DrawnCard card)
    {
        _cards.Add(card);
    }
}
