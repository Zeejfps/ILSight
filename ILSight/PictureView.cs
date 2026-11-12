using ILSight.Core.Resources;

namespace ILSight;

/// <summary>How a tab shows its picture: how far it is zoomed, and whether its markup shows instead.
/// Each new picture starts over at its own size, showing the picture.</summary>
public sealed class PictureView
{
    /// <summary>The picture being followed.</summary>
    public ResourceImage? Shown { get; set; }

    public PictureZoom Zoom { get; set; }

    /// <summary>Whether the tab shows the picture's markup instead of the picture.</summary>
    public bool ShowsSource { get; set; }
}
