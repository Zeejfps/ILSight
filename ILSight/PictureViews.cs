using ILSight.Core.Resources;

namespace ILSight;

public static class PictureViews
{
    /// <summary>Starts over when <paramref name="image"/>, the picture the tab shows or null, is
    /// another than the last, and lets go of one the tab no longer shows.</summary>
    public static void Follow(PictureView view, ResourceImage? image)
    {
        if (ReferenceEquals(image, view.Shown))
        {
            return;
        }

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
