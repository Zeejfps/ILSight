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

public static class PictureViews
{
    /// <summary>Starts over when <paramref name="image"/>, the picture the tab shows or null, is
    /// another than the last, and lets go of one the tab no longer shows.</summary>
    public static void Follow(PictureView view, ResourceImage? image)
    {
        if (ReferenceEquals(image, view.Shown))
            return;
        view.Shown = image;
        view.Zoom = default;
        view.ShowsSource = false;
    }

    /// <summary>Switches between the picture and its markup.</summary>
    public static void ToggleSource(PictureView view)
    {
        view.ShowsSource = !view.ShowsSource;
    }
}
