using ILSight.Core.Resources;

namespace ILSight;

/// <summary>How a tab shows its picture: how far it is zoomed, and whether its markup shows instead.
/// Each new picture starts over at its own size, showing the picture.</summary>
public sealed class PictureView
{
    private ResourceImage? _shown;

    public PictureZoom Zoom { get; private set; }

    /// <summary>Whether the tab shows the picture's markup instead of the picture.</summary>
    public bool ShowsSource { get; private set; }

    /// <summary>Starts over when <paramref name="image"/>, the picture the tab shows or null, is
    /// another than the last, and lets go of one the tab no longer shows.</summary>
    public void Follow(ResourceImage? image)
    {
        if (ReferenceEquals(image, _shown))
        {
            return;
        }

        _shown = image;
        Zoom = default;
        ShowsSource = false;
    }

    /// <summary>Switches between the picture and its markup.</summary>
    public void ToggleSource()
    {
        ShowsSource = !ShowsSource;
    }

    public void ZoomIn()
    {
        Zoom = Zoom.StepIn();
    }

    public void ZoomOut()
    {
        Zoom = Zoom.StepOut();
    }

    /// <summary>Shows the picture at its own size.</summary>
    public void ResetZoom()
    {
        Zoom = default;
    }
}
